namespace VLibrary.Domain.Enums
{
    /// <summary>Why a category row is only an old name and place that leads to a live category.</summary>
    public enum CategoryRedirect
    {
        Renamed = 0,
        Moved = 1,
        Merged = 2
    }
}
