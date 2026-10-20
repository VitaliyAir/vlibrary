namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// Книга <-> автор (многие-ко-многим) с порядком авторов на обложке.
    /// </summary>
    public class BookAuthorEntity
    {
        public long BookId { get; set; }
        public virtual BookEntity Book { get; set; } = null!;

        public long AuthorId { get; set; }
        public virtual AuthorInfoEntity Author { get; set; } = null!;

        public int Order { get; set; }
    }
}
