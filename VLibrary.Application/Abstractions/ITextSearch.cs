using VLibrary.Domain.Entities;

namespace VLibrary.Application.Abstractions
{
    /// <summary>
    /// Database full-text search (UC-4). Words are matched as prefixes, with stemming.
    /// </summary>
    public interface ITextSearch
    {
        /// <summary>Title, subtitle, ISBN, authors, tags and the text of the book's notes.</summary>
        IQueryable<BookEntity> FilterBooks(IQueryable<BookEntity> books, string text);

        IQueryable<BookNoteEntity> FilterNotes(IQueryable<BookNoteEntity> notes, string text);
    }
}
