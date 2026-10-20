namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// Key/value user settings (e.g. reminders on/off).
    /// </summary>
    public class AppSettingEntity
    {
        public required string Key { get; set; }
        public string Value { get; set; } = string.Empty;
    }
}
