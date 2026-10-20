using VLibrary.Application.Abstractions;
using VLibrary.Domain.Entities;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Books
{
    /// <summary>
    /// Creates a book from extracted metadata, reusing existing authors (matched by <see cref="PersonName.MatchKey"/>).
    /// The book is added to the context; the caller saves.
    /// </summary>
    public sealed class BookCreator(IAppDbContext db, CatalogResolvers resolvers)
    {
        public async Task<BookEntity> CreateAsync(ExtractedMetadata metadata, string fallbackTitle, CancellationToken cancellationToken)
        {
            var book = BookEntity.CreateFromMetadata(metadata, fallbackTitle);
            book.SetAuthors(await resolvers.ResolveAuthorsAsync(metadata.Authors, cancellationToken));
            book.RefreshSearchText();
            db.Books.Add(book);
            return book;
        }
    }
}
