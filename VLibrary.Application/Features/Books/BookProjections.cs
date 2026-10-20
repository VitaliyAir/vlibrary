using Microsoft.EntityFrameworkCore;
using VLibrary.Domain.Entities;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Books
{
    internal static class BookProjections
    {
        /// <summary>
        /// Loads list items for an already filtered, ordered and paged query (order is preserved).
        /// </summary>
        public static async Task<List<BookListItemDto>> ToListItemsAsync(this IQueryable<BookEntity> query, CancellationToken cancellationToken)
        {
            var rows = await query
                .AsNoTracking()
                .Select(b => new
                {
                    b.Id,
                    b.Title,
                    b.Subtitle,
                    b.YearPublished,
                    b.Rating,
                    b.ReadingStatus,
                    b.LastOpenedAt,
                    b.CreatedAt,
                    Authors = b.BookAuthors
                        .OrderBy(ba => ba.Order)
                        .Select(ba => new { ba.Author.LastName, ba.Author.FirstName, ba.Author.MiddleName, ba.Author.NameInitials })
                        .ToList(),
                    Tags = b.Tags.OrderBy(t => t.Name).Select(t => t.Name).ToList(),
                    Files = b.FilesMetas
                        .OrderByDescending(f => f.IsPrimary).ThenBy(f => f.Id)
                        .Select(f => new BookFileRow(
                            f.Id, f.FileName, f.CategoryId, f.StorageKey, f.Format, f.SizeBytes, f.PageCount, f.Status, f.IsPrimary,
                            f.Cover != null ? f.Cover.UpdatedAt : (DateTime?)null))
                        .ToList(),
                    NotesCount = b.Notes.Count
                })
                .ToListAsync(cancellationToken);

            return rows
                .Select(r => new BookListItemDto(
                    r.Id,
                    r.Title,
                    r.Subtitle,
                    r.Authors.Select(a => PersonName.Format(a.LastName, a.FirstName, a.MiddleName, a.NameInitials)).ToList(),
                    r.YearPublished,
                    r.Rating,
                    r.ReadingStatus,
                    r.LastOpenedAt,
                    r.CreatedAt,
                    r.Tags,
                    BookFileRow.ToDtos(r.Files),
                    BookFileRow.StateOf(r.Files),
                    r.NotesCount,
                    BookFileRow.CoverOf(r.Files)))
                .ToList();
        }

        public static IQueryable<BookEntity> ApplySort(this IQueryable<BookEntity> query, BookSort sort) => sort switch
        {
            BookSort.Added => query.OrderByDescending(b => b.CreatedAt).ThenBy(b => b.Id),
            BookSort.LastOpened => query.OrderByDescending(b => b.LastOpenedAt.HasValue).ThenByDescending(b => b.LastOpenedAt).ThenBy(b => b.Id),
            BookSort.Rating => query.OrderByDescending(b => b.Rating.HasValue).ThenByDescending(b => b.Rating).ThenBy(b => b.Title).ThenBy(b => b.Id),
            BookSort.Year => query.OrderByDescending(b => b.YearPublished.HasValue).ThenByDescending(b => b.YearPublished).ThenBy(b => b.Title).ThenBy(b => b.Id),
            _ => query.OrderBy(b => b.Title).ThenBy(b => b.Id)
        };
    }
}
