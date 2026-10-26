using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Books
{
    /// <summary>UC-3: the book card.</summary>
    public sealed record GetBookQuery(long Id) : IRequest<Result<BookDetailsDto>>;

    internal sealed class GetBookHandler(IAppDbContext db) : IRequestHandler<GetBookQuery, Result<BookDetailsDto>>
    {
        public async Task<Result<BookDetailsDto>> Handle(GetBookQuery request, CancellationToken cancellationToken)
        {
            var row = await db.Books
                .AsNoTracking()
                .Where(b => b.Id == request.Id)
                .Select(b => new
                {
                    Book = new
                    {
                        b.Id, b.Title, b.Subtitle, b.Description, b.ISBN, b.Edition, b.Publisher, b.YearPublished, b.TotalPages,
                        b.Language, b.TableOfContents, b.Glossary, b.Rating, b.ReadingStatus, b.LastOpenedAt, b.CreatedAt, b.UpdatedAt
                    },
                    Authors = b.BookAuthors
                        .OrderBy(ba => ba.Order)
                        .Select(ba => new { ba.AuthorId, ba.Author.LastName, ba.Author.FirstName, ba.Author.MiddleName, ba.Author.NameInitials })
                        .ToList(),
                    Tags = b.Tags.OrderBy(t => t.Name).Select(t => new NamedRefDto(t.Id, t.Name)).ToList(),
                    Files = b.FilesMetas
                        .OrderByDescending(f => f.IsPrimary).ThenBy(f => f.Id)
                        .Select(f => new BookFileRow(
                            f.Id, f.FileName, f.CategoryId, f.StorageKey, f.Format, f.SizeBytes, f.PageCount, f.Status, f.IsPrimary,
                            f.Cover != null ? f.Cover.UpdatedAt : (DateTime?)null))
                        .ToList(),
                    Shelves = b.ShelfBooks
                        .OrderBy(sb => sb.Bookshelf.SortOrder)
                        .Select(sb => new NamedRefDto(sb.BookshelfId, sb.Bookshelf.Name))
                        .ToList(),
                    NotesCount = b.Notes.Count
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (row == null)
            {
                return CommonErrors.NotFound("Book", request.Id);
            }

            var b = row.Book;
            return new BookDetailsDto(
                b.Id, b.Title, b.Subtitle, b.Description, b.ISBN, b.Edition, b.Publisher, b.YearPublished, b.TotalPages,
                b.Language, b.TableOfContents, b.Glossary, b.Rating, b.ReadingStatus, b.LastOpenedAt, b.CreatedAt, b.UpdatedAt,
                row.Authors.Select(a => new BookAuthorDto(
                    a.AuthorId, PersonName.Format(a.LastName, a.FirstName, a.MiddleName, a.NameInitials),
                    a.LastName, a.FirstName, a.MiddleName, a.NameInitials)).ToList(),
                row.Tags,
                BookFileRow.ToDtos(row.Files),
                row.Shelves,
                BookFileRow.StateOf(row.Files),
                row.NotesCount,
                BookFileRow.CoverOf(row.Files));
        }
    }
}
