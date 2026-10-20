namespace VLibrary.Domain.Entities
{
    public class ActionLog
    {
        public long ActionLogId { get; set; }
        public required string ActionById { get; set; }
        public required string EntityName { get; set; }
        public required string Action { get; set; }
        public string? Changes { get; set; }
        public string? EntityKey { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
