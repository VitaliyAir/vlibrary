using System.Text;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Books;
using VLibrary.Application.Features.Categories;
using VLibrary.Application.Features.Covers;
using VLibrary.Application.Features.Imports.Metadata;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;

namespace VLibrary.Application.Features.Imports
{
    /// <summary>
    /// Imports one uploaded file (UC-1) into VStorage + the catalog. Per file:
    /// <list type="number">
    /// <item>same category + file name, same content -> unchanged (counted)</item>
    /// <item>same category + file name, other content -> Conflict, skipped</item>
    /// <item>same content elsewhere in the library -> AlreadyInLibrary, skipped (not uploaded)</item>
    /// <item>same content as a deleted book / removed file -> DeletedDuplicateSkipped (reported once)</item>
    /// <item>new content -> uploaded; Imported (new book) or PendingReview (looks like an existing book)</item>
    /// </list>
    /// A storage outage is returned as an error (nothing recorded), so the browser can stop and retry the same file.
    /// </summary>
    internal sealed class ImportFileProcessor(
        IAppDbContext db,
        IUploadStaging staging,
        IBookMetadataReader metadataReader,
        IBookCandidateSource candidateSource,
        BookCreator bookCreator,
        CategoryResolver categories,
        CoverAttacher covers,
        IBookStorage storage,
        IClock clock)
    {
        public const int MaxKeyBytes = 1024;
        private const int MaxMessageLength = 2000;

        public async Task<Result<ImportFileResultDto>> ProcessAsync(
            ImportBatchEntity batch, ImportFileCommand command, CancellationToken cancellationToken)
        {
            var now = clock.UtcNow;
            var lastWriteTime = command.LastModifiedUtc ?? now;

            var parsed = ImportPath.Parse(command.RelativePath);
            if (parsed.IsFailure)
            {
                return await DoneAsync(batch, command.RelativePath, ImportOutcome.Failed, parsed.Error.Message, null);
            }

            var path = parsed.Value;
            var categoryPath = path.CategoryPath(batch.ParentCategory?.Path);
            var key = categoryPath.Length == 0 ? path.FileName : FileMetaEntity.StorageKeyFor(categoryPath, path.FileName);
            var rejected = Reject(path, categoryPath, key, command.RelativePath);
            if (rejected != null)
            {
                return await DoneAsync(batch, key, ImportOutcome.Failed, rejected, null);
            }

            var stagedResult = await staging.StageAsync(command.Content, path.FileName, cancellationToken);
            if (stagedResult.IsFailure)
            {
                return await DoneAsync(batch, key, ImportOutcome.Failed, stagedResult.Error.Message, null);
            }

            await using var staged = stagedResult.Value;

            var category = await categories.FindAsync(batch.ParentCategory, path.Folders, cancellationToken);
            if (category != null)
            {
                var byKey = await db.FileMetas.IgnoreQueryFilters()
                    .Include(f => f.Book)
                    .FirstOrDefaultAsync(f => f.CategoryId == category.Id && f.FileName == path.FileName, cancellationToken);
                if (byKey != null)
                {
                    return await HandleKnownKeyAsync(batch, key, byKey, staged, lastWriteTime, now);
                }
            }

            var sameContent = await db.FileMetas.IgnoreQueryFilters()
                .Include(f => f.Book)
                .Where(f => f.FileHash == staged.Sha256)
                .OrderBy(f => f.Id)
                .ToListAsync(cancellationToken);
            if (sameContent.Count > 0)
            {
                return await HandleKnownContentAsync(batch, key, sameContent, now);
            }

            return await ImportNewAsync(batch, path, key, staged, lastWriteTime, now, cancellationToken);
        }

        /// <summary>Cases 1, 2 and a tombstone at the same name.</summary>
        private async Task<Result<ImportFileResultDto>> HandleKnownKeyAsync(
            ImportBatchEntity batch, string key, FileMetaEntity byKey, IStagedFile staged, DateTime lastWriteTime, DateTime now)
        {
            if (byKey.IsDeleted)
            {
                return await ReportDeletedAsync(batch, key, byKey, now);
            }

            if (byKey.FileHash == staged.Sha256)
            {
                byKey.Touch(staged.SizeBytes, lastWriteTime, now);
                batch.RecordUnchanged();
                return await DoneAsync(batch, key, null, null, byKey);
            }

            // D4: the name now holds other content. The record and the VStorage object keep the original file.
            return await DoneAsync(batch, key, ImportOutcome.Conflict,
                $"Another file with this name is already in the library (was {byKey.SizeBytes:N0} bytes, SHA-256 {byKey.FileHash[..12]}…; " +
                $"now {staged.SizeBytes:N0} bytes, {staged.Sha256[..12]}…). Nothing was changed" +
                (byKey.Book != null ? $" (book «{byKey.Book.Title}», #{byKey.Book.Id})." : "."),
                byKey);
        }

        /// <summary>Cases 3 and 4: the content is already known under another name or category.</summary>
        private async Task<Result<ImportFileResultDto>> HandleKnownContentAsync(
            ImportBatchEntity batch, string key, List<FileMetaEntity> sameContent, DateTime now)
        {
            var live = sameContent
                .Where(f => !f.IsDeleted && f.Book is not { IsDeleted: true })
                .OrderByDescending(f => f.BookId.HasValue).ThenByDescending(f => f.IsPrimary)
                .FirstOrDefault();
            if (live != null)
            {
                var where = live.Book != null
                    ? $" (book «{live.Book.Title}», #{live.Book.Id})."
                    : " (waiting for duplicate review or not linked to a book).";
                return await DoneAsync(batch, key, ImportOutcome.AlreadyInLibrary,
                    $"The same content is already in the library: {live.StorageKey}{where} Not uploaded again.", live);
            }

            // D2: only tombstones (removed files / files of deleted books) have this content.
            var tombstone = sameContent.FirstOrDefault(f => f.Book != null) ?? sameContent[0];
            return await ReportDeletedAsync(batch, key, tombstone, now);
        }

        /// <summary>Case 5: new content -> VStorage, then the catalog.</summary>
        private async Task<Result<ImportFileResultDto>> ImportNewAsync(
            ImportBatchEntity batch, ImportPath path, string key, IStagedFile staged, DateTime lastWriteTime, DateTime now,
            CancellationToken cancellationToken)
        {
            var metadata = await metadataReader.ReadAsync(staged.Path, cancellationToken);

            Result<StoredObjectInfo> uploaded;
            await using (var content = staged.OpenRead())
            {
                uploaded = await storage.UploadAsync(key, path.FileName, content, staged.SizeBytes, cancellationToken);
            }

            if (uploaded.IsFailure)
            {
                if (uploaded.Error == StorageErrors.KeyTaken)
                {
                    return await DoneAsync(batch, key, ImportOutcome.Conflict,
                        "VStorage already has another file under this name that the library does not know. Nothing was changed.", null);
                }

                if (uploaded.Error.Type == ErrorType.Unavailable)
                {
                    return uploaded.Error;
                }

                return await DoneAsync(batch, key, ImportOutcome.Failed, uploaded.Error.Message, null);
            }

            var category = await categories.GetOrCreateAsync(batch.ParentCategory, path.Folders, cancellationToken);
            var file = FileMetaEntity.Create(
                category, path.FileName, staged.SizeBytes, lastWriteTime, staged.Sha256, uploaded.Value.ObjectId, now);
            file.ExtractedMetadata = metadata;
            file.PageCount = metadata.PageCount;

            // The title the new book would get (see BookEntity.CreateFromMetadata) and the title stored inside the file.
            var fileNameTitle = Path.GetFileNameWithoutExtension(path.FileName);
            var title = string.IsNullOrWhiteSpace(metadata.Title) ? fileNameTitle : metadata.Title.Trim();
            var hasContentTitle = metadata.Source != FileNameParser.SourceName && !string.IsNullOrWhiteSpace(metadata.Title);
            file.ContentTitleKey = hasContentTitle ? TextNormalizer.WordsKey(metadata.Title) : null;
            db.FileMetas.Add(file);

            // The cover comes from the temp copy while it is here (first page / embedded cover image).
            await covers.AttachAsync(file, staged.Path, cancellationToken);

            var candidates = await candidateSource.FindAsync(
                new DuplicateQuery(
                    Isbn.Normalize(metadata.Isbn),
                    TextNormalizer.Normalize(title),
                    TextNormalizer.WordsKey(title),
                    file.ContentTitleKey,
                    file.Extension,
                    file.SizeBytes),
                cancellationToken);
            var matches = DuplicateMatcher.Match(candidates);

            ImportOutcome outcome;
            string? message = null;
            if (matches.Count == 0)
            {
                file.Book = await bookCreator.CreateAsync(metadata, fileNameTitle, cancellationToken);
                file.MakePrimary();
                outcome = ImportOutcome.Imported;
            }
            else
            {
                foreach (var match in matches)
                {
                    file.DuplicateCandidates.Add(new DuplicateCandidateEntity
                    {
                        FileMeta = file,
                        CandidateBookId = match.BookId,
                        Score = match.Score,
                        Reason = match.Reason
                    });
                }

                outcome = ImportOutcome.PendingReview;
                message = $"{matches.Count} possible match(es) with existing books.";
            }

            try
            {
                return await DoneAsync(batch, key, outcome, message, file);
            }
            catch when (uploaded.Value.Created)
            {
                // Compensation: the catalog does not know the object, so it must not stay in VStorage.
                await storage.DeleteAsync(uploaded.Value.ObjectId, CancellationToken.None);
                throw;
            }
        }

        /// <summary>
        /// D2, reported once: a report line the first time, later imports only count the file as
        /// "deleted book, already reported".
        /// </summary>
        private async Task<Result<ImportFileResultDto>> ReportDeletedAsync(
            ImportBatchEntity batch, string key, FileMetaEntity tombstone, DateTime now)
        {
            if (TryCountKnownDeleted(batch, tombstone))
            {
                return await DoneAsync(batch, key, null, null, tombstone);
            }

            tombstone.MarkDeletedReported(now);
            return await DoneAsync(batch, key, ImportOutcome.DeletedDuplicateSkipped, DeletedMessage(tombstone), tombstone);
        }

        /// <summary>True (and counted) when an earlier import already reported this tombstone.</summary>
        internal static bool TryCountKnownDeleted(ImportBatchEntity batch, FileMetaEntity tombstone)
        {
            if (tombstone.DeletedReportedAt is { } reportedAt && reportedAt < batch.StartedAt)
            {
                batch.RecordKnownDeleted();
                return true;
            }

            return false;
        }

        internal static string DeletedMessage(FileMetaEntity tombstone) =>
            tombstone.Book is { IsDeleted: true } book
                ? $"Same content as deleted book «{book.Title}» (#{book.Id}): skipped."
                : $"Same content as a file removed from the library ({tombstone.StorageKey}): skipped.";

        private static string? Reject(ImportPath path, string categoryPath, string key, string relativePath)
        {
            if (!FileFormats.IsSupported(path.FileName))
            {
                return $"Unsupported file type '{Path.GetExtension(path.FileName)}'. " +
                       $"Supported: {string.Join(", ", FileFormats.SupportedExtensions)}.";
            }

            if (categoryPath.Length == 0)
            {
                return ImportErrors.NoCategory(relativePath).Message;
            }

            if (categoryPath.Length > CategoryEntity.MaxPathLength || Encoding.UTF8.GetByteCount(key) > MaxKeyBytes)
            {
                return $"The path is too long for the library (category path up to {CategoryEntity.MaxPathLength} characters, " +
                       $"category path + file name up to {MaxKeyBytes} bytes).";
            }

            return null;
        }

        /// <summary>Records the outcome (null = unchanged / already counted), saves, returns the progress.</summary>
        private async Task<Result<ImportFileResultDto>> DoneAsync(
            ImportBatchEntity batch, string path, ImportOutcome? outcome, string? message, FileMetaEntity? file)
        {
            if (outcome.HasValue)
            {
                batch.RecordOutcome(outcome.Value);
                db.ImportItems.Add(NewItem(batch.Id, path, outcome.Value, message, file));
            }

            await db.SaveChangesAsync(CancellationToken.None);
            return new ImportFileResultDto(path, outcome, message, file?.Id, file?.BookId, ImportBatchDto.From(batch));
        }

        internal static ImportItemEntity NewItem(long batchId, string path, ImportOutcome outcome, string? message, FileMetaEntity? file) =>
            new()
            {
                ImportBatchId = batchId,
                Path = path.Length > 1000 ? path[..1000] : path,
                Outcome = outcome,
                Message = message?.Length > MaxMessageLength ? message[..MaxMessageLength] : message,
                FileMeta = file
            };
    }
}
