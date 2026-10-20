namespace VLibrary.Domain.Entities
{
    public class BookshelfBookEntity
    {
        public long BookshelfId { get; set; }
        public virtual BookshelfEntity Bookshelf { get; set; } = null!;

        public long BookId { get; set; }
        public virtual BookEntity Book { get; set; } = null!;

        public int Position { get; set; }
        public DateTime AddedAt { get; set; }
    }
}
