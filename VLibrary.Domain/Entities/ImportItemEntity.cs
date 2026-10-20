using VLibrary.Domain.Enums;

namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// Result for one file of an import batch (unchanged files are only counted, not logged).
    /// </summary>
    public class ImportItemEntity : AuditableEntity
    {
        public long ImportBatchId { get; set; }
        public virtual ImportBatchEntity ImportBatch { get; set; } = null!;

        /// <summary>The uploaded file's path from the category start, e.g. "Library/Math/Geometry.pdf".</summary>
        public string Path { get; set; } = string.Empty;
        public ImportOutcome Outcome { get; set; }
        public string? Message { get; set; }

        public long? FileMetaId { get; set; }
        public virtual FileMetaEntity? FileMeta { get; set; }
    }
}
