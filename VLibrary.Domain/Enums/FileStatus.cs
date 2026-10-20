namespace VLibrary.Domain.Enums
{
    public enum FileStatus
    {
        Ok = 0,

        /// <summary>The object is no longer in VStorage (found by the library check).</summary>
        Missing = 1
    }
}
