namespace VLibrary.Domain.Enums
{
    public enum ImportStatus
    {
        /// <summary>The browser is uploading the files.</summary>
        Running = 0,
        Completed = 1,
        Cancelled = 2,
        Failed = 3
    }
}
