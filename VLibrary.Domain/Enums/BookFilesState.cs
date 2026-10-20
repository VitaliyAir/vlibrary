namespace VLibrary.Domain.Enums
{
    /// <summary>Availability of a book's files, shown as a colour in the UI.</summary>
    public enum BookFilesState
    {
        NoFiles = 0,
        Ok = 1,

        /// <summary>Some files are missing in VStorage.</summary>
        Partial = 2,

        /// <summary>No file can be opened.</summary>
        Missing = 3
    }
}
