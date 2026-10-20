namespace VLibrary.Domain.Entities
{
    public class BookClasificatorEntity : AuditableEntity
    {
        public string Title { get; set; } = string.Empty;
        public long? ParentId { get; set; }
        public virtual BookClasificatorEntity? Parent { get; set; }
        public virtual ICollection<BookClasificatorEntity> Children { get; set; } = new List<BookClasificatorEntity>();
        public virtual ICollection<BookEntity> Books { get; set; } = new List<BookEntity>();
    }
}
