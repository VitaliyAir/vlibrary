using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Categories;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Imports
{
    /// <summary>
    /// UC-1, step 1: the browser starts an import of <paramref name="TotalFiles"/> files picked from a folder.
    /// <paramref name="SourceName"/> is shown in the import list (e.g. "Library/Math").
    /// </summary>
    public sealed record CreateImportCommand(string SourceName, int TotalFiles, long? ParentCategoryId) : IRequest<Result<long>>;

    /// <summary>
    /// UC-1, step 2 (optional, up to 1000 files per call): files already in the library under the same category and name,
    /// with the same size and modification time, are counted without uploading them.
    /// </summary>
    public sealed record PrecheckImportCommand(long ImportId, IReadOnlyList<PrecheckFileDto> Files) : IRequest<Result<PrecheckResultDto>>;

    /// <summary>UC-1, step 3: one uploaded file. <paramref name="Content"/> is the request stream (not buffered).</summary>
    public sealed record ImportFileCommand(long ImportId, string RelativePath, DateTime? LastModifiedUtc, Stream Content)
        : IRequest<Result<ImportFileResultDto>>;

    /// <summary>UC-1, step 4: all files were sent.</summary>
    public sealed record CompleteImportCommand(long ImportId) : IRequest<Result<ImportBatchDto>>;

    /// <summary>The user stopped the upload.</summary>
    public sealed record CancelImportCommand(long ImportId) : IRequest<Result>;

    internal sealed class CreateImportValidator : AbstractValidator<CreateImportCommand>
    {
        public CreateImportValidator()
        {
            RuleFor(c => c.SourceName).NotEmpty().MaximumLength(1000);
            RuleFor(c => c.TotalFiles).InclusiveBetween(0, 1_000_000);
        }
    }

    internal sealed class PrecheckImportValidator : AbstractValidator<PrecheckImportCommand>
    {
        public const int MaxFiles = 1000;

        public PrecheckImportValidator()
        {
            RuleFor(c => c.Files).NotNull().Must(f => f.Count <= MaxFiles).WithMessage($"Up to {MaxFiles} files per precheck.");
            RuleForEach(c => c.Files).ChildRules(file =>
            {
                file.RuleFor(f => f.RelativePath).NotEmpty().MaximumLength(1000);
                file.RuleFor(f => f.Size).GreaterThanOrEqualTo(0);
            });
        }
    }

    internal sealed class ImportFileValidator : AbstractValidator<ImportFileCommand>
    {
        public ImportFileValidator()
        {
            RuleFor(c => c.RelativePath).NotEmpty().MaximumLength(1000);
            RuleFor(c => c.Content).NotNull();
        }
    }

    internal sealed class ImportCommandHandlers(
        IAppDbContext db,
        ImportFileProcessor processor,
        CategoryResolver categories,
        IClock clock,
        ILogger<ImportCommandHandlers> logger)
        : IRequestHandler<CreateImportCommand, Result<long>>,
          IRequestHandler<PrecheckImportCommand, Result<PrecheckResultDto>>,
          IRequestHandler<ImportFileCommand, Result<ImportFileResultDto>>,
          IRequestHandler<CompleteImportCommand, Result<ImportBatchDto>>,
          IRequestHandler<CancelImportCommand, Result>
    {
        public async Task<Result<long>> Handle(CreateImportCommand request, CancellationToken cancellationToken)
        {
            CategoryEntity? parent = null;
            if (request.ParentCategoryId is { } parentId)
            {
                parent = await db.Categories.FirstOrDefaultAsync(c => c.Id == parentId, cancellationToken);
                if (parent == null)
                {
                    return ImportErrors.ParentCategoryNotFound(parentId);
                }
            }

            var batch = ImportBatchEntity.Create(request.SourceName, request.TotalFiles, parent, clock.UtcNow);
            db.ImportBatches.Add(batch);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Import {BatchId} started: {Source}, {Count} files, parent category {Parent}",
                batch.Id, batch.SourceName, batch.TotalFiles, parent?.Path ?? "(top level)");
            return batch.Id;
        }

        public async Task<Result<PrecheckResultDto>> Handle(PrecheckImportCommand request, CancellationToken cancellationToken)
        {
            var batch = await LoadRunningAsync(request.ImportId, cancellationToken);
            if (batch.IsFailure)
            {
                return batch.Error;
            }

            var now = clock.UtcNow;
            var resolved = new Dictionary<string, CategoryEntity?>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<(PrecheckFileDto File, CategoryEntity Category, string FileName)>();
            foreach (var file in request.Files)
            {
                var path = ImportPath.Parse(file.RelativePath);
                if (path.IsFailure)
                {
                    continue;
                }

                var folders = string.Join(CategoryEntity.Separator, path.Value.Folders);
                if (!resolved.TryGetValue(folders, out var category))
                {
                    category = await categories.FindAsync(batch.Value.ParentCategory, path.Value.Folders, cancellationToken);
                    resolved[folders] = category;
                }

                if (category != null)
                {
                    candidates.Add((file, category, path.Value.FileName));
                }
            }

            var categoryIds = candidates.Select(c => c.Category.Id).Distinct().ToList();
            var names = candidates.Select(c => c.FileName).Distinct().ToList();
            var known = (await db.FileMetas.IgnoreQueryFilters()
                    .Include(f => f.Book)
                    .Where(f => categoryIds.Contains(f.CategoryId) && names.Contains(f.FileName))
                    .ToListAsync(cancellationToken))
                .ToDictionary(f => (f.CategoryId, f.FileName));

            var skip = new List<string>();
            foreach (var (file, category, fileName) in candidates)
            {
                var lastWriteTime = DateTimeOffset.FromUnixTimeMilliseconds(file.LastModified).UtcDateTime;
                if (!known.TryGetValue((category.Id, fileName), out var existing) || !existing.IsUnchanged(file.Size, lastWriteTime))
                {
                    continue;
                }

                if (!existing.IsDeleted)
                {
                    existing.MarkSeen(now);
                    batch.Value.RecordUnchanged();
                }
                else if (!ImportFileProcessor.TryCountKnownDeleted(batch.Value, existing))
                {
                    existing.MarkDeletedReported(now);
                    batch.Value.RecordOutcome(ImportOutcome.DeletedDuplicateSkipped);
                    db.ImportItems.Add(ImportFileProcessor.NewItem(
                        batch.Value.Id, existing.StorageKey, ImportOutcome.DeletedDuplicateSkipped,
                        ImportFileProcessor.DeletedMessage(existing), existing));
                }

                skip.Add(file.RelativePath);
            }

            await db.SaveChangesAsync(cancellationToken);
            return new PrecheckResultDto(skip, ImportBatchDto.From(batch.Value));
        }

        public async Task<Result<ImportFileResultDto>> Handle(ImportFileCommand request, CancellationToken cancellationToken)
        {
            var batch = await LoadRunningAsync(request.ImportId, cancellationToken);
            if (batch.IsFailure)
            {
                return batch.Error;
            }

            try
            {
                var result = await processor.ProcessAsync(batch.Value, request, cancellationToken);
                if (result.IsFailure)
                {
                    // Storage outage: nothing is recorded, the browser may retry this file.
                    db.ClearChangeTracker();
                }

                return result;
            }
            catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(ex, "Import {BatchId}: failed to import {Path}", request.ImportId, request.RelativePath);
                db.ClearChangeTracker();
                var reloaded = await db.ImportBatches.Include(b => b.ParentCategory)
                    .FirstAsync(b => b.Id == request.ImportId, CancellationToken.None);
                var message = ex.GetBaseException().Message;
                reloaded.RecordOutcome(ImportOutcome.Failed);
                db.ImportItems.Add(ImportFileProcessor.NewItem(reloaded.Id, request.RelativePath, ImportOutcome.Failed, message, null));
                await db.SaveChangesAsync(CancellationToken.None);
                return new ImportFileResultDto(request.RelativePath, ImportOutcome.Failed, message, null, null, ImportBatchDto.From(reloaded));
            }
        }

        public async Task<Result<ImportBatchDto>> Handle(CompleteImportCommand request, CancellationToken cancellationToken)
        {
            var batch = await LoadRunningAsync(request.ImportId, cancellationToken);
            if (batch.IsFailure)
            {
                return batch.Error;
            }

            batch.Value.Complete(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Import {BatchId} completed: imported {Imported}, unchanged {Unchanged}, already in library {Already}, review {Review}, " +
                "deleted duplicates {Deleted}, conflicts {Conflicts}, failed {Failed}",
                batch.Value.Id, batch.Value.ImportedCount, batch.Value.UnchangedCount, batch.Value.AlreadyInLibraryCount,
                batch.Value.PendingReviewCount, batch.Value.DeletedDuplicateCount, batch.Value.ConflictCount, batch.Value.FailedCount);
            return ImportBatchDto.From(batch.Value);
        }

        public async Task<Result> Handle(CancelImportCommand request, CancellationToken cancellationToken)
        {
            var batch = await LoadRunningAsync(request.ImportId, cancellationToken);
            if (batch.IsFailure)
            {
                return batch.Error;
            }

            batch.Value.Cancel(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private async Task<Result<ImportBatchEntity>> LoadRunningAsync(long id, CancellationToken cancellationToken)
        {
            var batch = await db.ImportBatches.Include(b => b.ParentCategory).FirstOrDefaultAsync(b => b.Id == id, cancellationToken);
            if (batch == null)
            {
                return ImportErrors.NotFound(id);
            }

            return batch.IsFinished ? ImportErrors.AlreadyFinished(id) : batch;
        }
    }
}
