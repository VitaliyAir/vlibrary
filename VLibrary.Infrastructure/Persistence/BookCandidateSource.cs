using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;

namespace VLibrary.Infrastructure.Persistence
{
    /// <summary>
    /// Exact lookups for the duplicate rules (see DuplicateMatcher): ISBN, normalized title, title words,
    /// title stored inside a file, extension + size. Soft-deleted books and files are excluded by query filters.
    /// </summary>
    internal sealed class BookCandidateSource(AppDbContext db) : IBookCandidateSource
    {
        private const int MaxPerRule = 20;

        public async Task<IReadOnlyList<BookCandidate>> FindAsync(DuplicateQuery query, CancellationToken cancellationToken)
        {
            var byIsbn = string.IsNullOrEmpty(query.Isbn13)
                ? []
                : await BookIds(db.Books.Where(b => b.ISBN == query.Isbn13), cancellationToken);

            var byTitle = query.TitleKey.Length == 0
                ? []
                : await BookIds(db.Books.Where(b => b.NormalizedTitle == query.TitleKey), cancellationToken);

            var byWords = query.TitleWordsKey.Length == 0
                ? []
                : await BookIds(db.Books.Where(b => b.TitleWordsKey == query.TitleWordsKey), cancellationToken);

            var byContentTitle = string.IsNullOrEmpty(query.ContentTitleKey)
                ? []
                : await FileBookIds(db.FileMetas.Where(f => f.ContentTitleKey == query.ContentTitleKey), cancellationToken);

            var bySize = await FileBookIds(
                db.FileMetas.Where(f => f.Extension == query.Extension && f.SizeBytes == query.SizeBytes),
                cancellationToken);

            return byIsbn.Concat(byTitle).Concat(byWords).Concat(byContentTitle).Concat(bySize)
                .Distinct()
                .Select(id => new BookCandidate(
                    id,
                    byIsbn.Contains(id),
                    byTitle.Contains(id),
                    byWords.Contains(id),
                    byContentTitle.Contains(id),
                    bySize.Contains(id)))
                .ToList();
        }

        private static async Task<HashSet<long>> BookIds(IQueryable<Domain.Entities.BookEntity> books, CancellationToken cancellationToken) =>
            (await books.OrderBy(b => b.Id).Select(b => b.Id).Take(MaxPerRule).ToListAsync(cancellationToken)).ToHashSet();

        /// <summary>Books of matching files (files waiting for review have no book and are skipped).</summary>
        private static async Task<HashSet<long>> FileBookIds(IQueryable<Domain.Entities.FileMetaEntity> files, CancellationToken cancellationToken) =>
            (await files
                .Where(f => f.BookId != null && !f.Book!.IsDeleted)
                .Select(f => f.BookId!.Value)
                .Distinct()
                .OrderBy(id => id)
                .Take(MaxPerRule)
                .ToListAsync(cancellationToken))
            .ToHashSet();
    }
}
