using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Books;
using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.ReadingRoom
{
    public sealed record ReadingRoomShelfDto(long Id, string Name, int BooksCount, IReadOnlyList<BookListItemDto> Books);

    public sealed record ReadingRoomDto(
        IReadOnlyList<BookListItemDto> RecentlyOpened,
        IReadOnlyList<BookListItemDto> InProgress,
        IReadOnlyList<ReadingRoomShelfDto> Shelves);

    /// <summary>UC-5: book lists of the reading room.</summary>
    public sealed record GetReadingRoomQuery(int BooksPerList = 12) : IRequest<Result<ReadingRoomDto>>;

    internal sealed class GetReadingRoomHandler(IAppDbContext db) : IRequestHandler<GetReadingRoomQuery, Result<ReadingRoomDto>>
    {
        public async Task<Result<ReadingRoomDto>> Handle(GetReadingRoomQuery request, CancellationToken cancellationToken)
        {
            var take = Math.Clamp(request.BooksPerList, 1, 50);

            var recentlyOpened = await db.Books
                .Where(b => b.LastOpenedAt != null)
                .OrderByDescending(b => b.LastOpenedAt)
                .Take(take)
                .ToListItemsAsync(cancellationToken);

            var inProgress = await db.Books
                .Where(b => b.ReadingStatus == ReadingStatus.InProgress)
                .OrderByDescending(b => b.LastOpenedAt.HasValue).ThenByDescending(b => b.LastOpenedAt).ThenBy(b => b.Title)
                .Take(take)
                .ToListItemsAsync(cancellationToken);

            var shelves = await db.Bookshelves.AsNoTracking()
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Select(s => new { s.Id, s.Name, BooksCount = s.ShelfBooks.Count(sb => !sb.Book.IsDeleted) })
                .ToListAsync(cancellationToken);

            var shelfDtos = new List<ReadingRoomShelfDto>();
            foreach (var shelf in shelves)
            {
                var books = await db.BookshelfBooks
                    .Where(sb => sb.BookshelfId == shelf.Id)
                    .OrderBy(sb => sb.Position).ThenBy(sb => sb.AddedAt)
                    .Select(sb => sb.Book)
                    .Take(take)
                    .ToListItemsAsync(cancellationToken);
                shelfDtos.Add(new ReadingRoomShelfDto(shelf.Id, shelf.Name, shelf.BooksCount, books));
            }

            return new ReadingRoomDto(recentlyOpened, inProgress, shelfDtos);
        }
    }
}
