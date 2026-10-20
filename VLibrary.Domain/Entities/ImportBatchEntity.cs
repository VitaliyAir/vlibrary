using VLibrary.Domain.Enums;

namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// One import run (UC-1): files of a folder picked in the browser, uploaded one by one to VStorage.
    /// </summary>
    public class ImportBatchEntity : AuditableEntity
    {
        public const string InterruptedMessage = "Interrupted: the upload stopped before the import was completed.";

        /// <summary>What was imported, e.g. "Library/Math" (the picked folder from the chosen category start).</summary>
        public string SourceName { get; private set; } = string.Empty;

        /// <summary>The existing category the imported folders were placed under; null = top level.</summary>
        public long? ParentCategoryId { get; private set; }
        public virtual CategoryEntity? ParentCategory { get; set; }

        public ImportStatus Status { get; private set; } = ImportStatus.Running;
        public DateTime? StartedAt { get; private set; }
        public DateTime? FinishedAt { get; private set; }
        public string? Error { get; private set; }

        /// <summary>Files the browser is going to send (announced when the import starts).</summary>
        public int TotalFiles { get; private set; }
        public int ProcessedFiles { get; private set; }
        public int UnchangedCount { get; private set; }
        public int ImportedCount { get; private set; }
        public int AlreadyInLibraryCount { get; private set; }
        public int ConflictCount { get; private set; }
        public int DeletedDuplicateCount { get; private set; }

        /// <summary>Files of deleted books that earlier imports already reported (counted, no report line).</summary>
        public int KnownDeletedCount { get; private set; }
        public int PendingReviewCount { get; private set; }
        public int FailedCount { get; private set; }

        public virtual ICollection<ImportItemEntity> Items { get; set; } = new List<ImportItemEntity>();

        public bool IsFinished => Status is ImportStatus.Completed or ImportStatus.Cancelled or ImportStatus.Failed;

        public static ImportBatchEntity Create(string sourceName, int totalFiles, CategoryEntity? parentCategory, DateTime utcNow) =>
            new()
            {
                SourceName = sourceName.Trim(),
                TotalFiles = Math.Max(totalFiles, 0),
                ParentCategory = parentCategory,
                ParentCategoryId = parentCategory?.Id,
                Status = ImportStatus.Running,
                StartedAt = utcNow
            };

        public void RecordKnownDeleted()
        {
            KnownDeletedCount++;
            ProcessedFiles++;
        }

        public void RecordUnchanged(int count = 1)
        {
            UnchangedCount += count;
            ProcessedFiles += count;
        }

        public void RecordOutcome(ImportOutcome outcome)
        {
            switch (outcome)
            {
                case ImportOutcome.Imported: ImportedCount++; break;
                case ImportOutcome.PendingReview: PendingReviewCount++; break;
                case ImportOutcome.AlreadyInLibrary: AlreadyInLibraryCount++; break;
                case ImportOutcome.Conflict: ConflictCount++; break;
                case ImportOutcome.DeletedDuplicateSkipped: DeletedDuplicateCount++; break;
                case ImportOutcome.Failed: FailedCount++; break;
                default: throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null);
            }

            ProcessedFiles++;
        }

        public void Complete(DateTime utcNow) => Finish(ImportStatus.Completed, utcNow, null);

        public void Cancel(DateTime utcNow) => Finish(ImportStatus.Cancelled, utcNow, null);

        /// <summary>The browser stopped sending (tab closed, API restarted) and never completed the import.</summary>
        public void Interrupt(DateTime utcNow) => Finish(ImportStatus.Cancelled, utcNow, InterruptedMessage);

        public void Fail(string error, DateTime utcNow) => Finish(ImportStatus.Failed, utcNow, error);

        private void Finish(ImportStatus status, DateTime utcNow, string? error)
        {
            Status = status;
            FinishedAt = utcNow;
            Error = error;
        }
    }
}
