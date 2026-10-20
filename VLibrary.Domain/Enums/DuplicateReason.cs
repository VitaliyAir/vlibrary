namespace VLibrary.Domain.Enums
{
    /// <summary>Why an imported file may be another copy of an existing book (score in brackets).</summary>
    public enum DuplicateReason
    {
        /// <summary>Same ISBN (100%).</summary>
        IsbnMatch = 0,

        /// <summary>Same normalized title (100%).</summary>
        TitleMatch = 1,

        /// <summary>Same title words in another order (90%).</summary>
        TitleWordsMatch = 2,

        /// <summary>Same title stored inside the files (90%).</summary>
        ContentTitleMatch = 3,

        /// <summary>Same extension and same file size (80%).</summary>
        SizeMatch = 4
    }
}
