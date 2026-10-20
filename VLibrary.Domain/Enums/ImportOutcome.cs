namespace VLibrary.Domain.Enums
{
    /// <summary>Result of one uploaded file (unchanged files are only counted).</summary>
    public enum ImportOutcome
    {
        /// <summary>New content: uploaded to VStorage, new book.</summary>
        Imported = 0,

        /// <summary>New content that looks like an existing book: uploaded, waits for duplicate review.</summary>
        PendingReview = 1,

        /// <summary>The same content is already in the library in another category: skipped, not uploaded.</summary>
        AlreadyInLibrary = 2,

        /// <summary>The category already has a file with this name but other content: skipped.</summary>
        Conflict = 3,

        /// <summary>Same content as a file of a deleted book (or a removed file): skipped.</summary>
        DeletedDuplicateSkipped = 4,

        /// <summary>Unsupported file, upload or storage error.</summary>
        Failed = 5
    }
}
