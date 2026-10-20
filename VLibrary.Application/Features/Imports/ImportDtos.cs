using System.Linq.Expressions;
using VLibrary.Application.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Imports
{
    public sealed record ImportBatchDto(
        long Id,
        string SourceName,
        long? ParentCategoryId,
        string? ParentCategoryPath,
        ImportStatus Status,
        DateTime CreatedAt,
        DateTime? StartedAt,
        DateTime? FinishedAt,
        string? Error,
        int TotalFiles,
        int ProcessedFiles,
        int UnchangedCount,
        int ImportedCount,
        int AlreadyInLibraryCount,
        int ConflictCount,
        int DeletedDuplicateCount,
        int KnownDeletedCount,
        int PendingReviewCount,
        int FailedCount)
    {
        internal static readonly Expression<Func<ImportBatchEntity, ImportBatchDto>> Projection = b => new ImportBatchDto(
            b.Id, b.SourceName, b.ParentCategoryId, b.ParentCategory != null ? b.ParentCategory.Path : null,
            b.Status, b.CreatedAt, b.StartedAt, b.FinishedAt, b.Error,
            b.TotalFiles, b.ProcessedFiles, b.UnchangedCount, b.ImportedCount, b.AlreadyInLibraryCount,
            b.ConflictCount, b.DeletedDuplicateCount, b.KnownDeletedCount, b.PendingReviewCount, b.FailedCount);

        private static readonly Func<ImportBatchEntity, ImportBatchDto> FromEntity = Projection.Compile();

        /// <summary>Progress of a tracked batch (ParentCategoryPath only when the parent is loaded).</summary>
        internal static ImportBatchDto From(ImportBatchEntity batch) => FromEntity(batch);
    }

    public sealed record ImportItemDto(long Id, string Path, ImportOutcome Outcome, string? Message, long? FileMetaId, long? BookId);

    public sealed record ImportBatchDetailsDto(ImportBatchDto Batch, PagedList<ImportItemDto> Items);

    /// <summary>Result of one uploaded file; <paramref name="Outcome"/> null = unchanged (only counted).</summary>
    public sealed record ImportFileResultDto(
        string Path, ImportOutcome? Outcome, string? Message, long? FileMetaId, long? BookId, ImportBatchDto Batch);

    /// <param name="LastModified">Modification time as the browser reports it (Unix milliseconds).</param>
    public sealed record PrecheckFileDto(string RelativePath, long Size, long LastModified);

    /// <param name="Skip">Relative paths that need no upload (unchanged, or an already reported deleted book).</param>
    public sealed record PrecheckResultDto(IReadOnlyList<string> Skip, ImportBatchDto Batch);
}
