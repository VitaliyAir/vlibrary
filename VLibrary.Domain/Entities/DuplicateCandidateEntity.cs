using VLibrary.Domain.Enums;

namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// UC-2: an imported file that may belong to an existing book. Waits in the review queue
    /// until the user links it, creates a new book or removes the file.
    /// </summary>
    public class DuplicateCandidateEntity : AuditableEntity
    {
        public long FileMetaId { get; set; }
        public virtual FileMetaEntity FileMeta { get; set; } = null!;

        public long CandidateBookId { get; set; }
        public virtual BookEntity CandidateBook { get; set; } = null!;

        /// <summary>0..1, where 1 is an exact ISBN match.</summary>
        public double Score { get; set; }
        public DuplicateReason Reason { get; set; }
        public DateTime? ResolvedAt { get; private set; }

        public void Resolve(DateTime utcNow) => ResolvedAt ??= utcNow;
    }
}
