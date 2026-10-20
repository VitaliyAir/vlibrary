namespace VLibrary.Domain.Entities
{
    public class BookshelfEntity : AuditableEntity
    {
        public required string Name { get; set; }
        public string? Description { get; set; }
        public int SortOrder { get; set; }

        // Связь многие-ко-многим с книгами (упорядоченная)
        public virtual ICollection<BookshelfBookEntity> ShelfBooks { get; set; } = new List<BookshelfBookEntity>();
    }
}
