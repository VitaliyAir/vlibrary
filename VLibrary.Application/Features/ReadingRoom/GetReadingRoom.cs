using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Books;
using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.ReadingRoom
{
    public sealed record ReadingRoomDto(IReadOnlyList<BookListItemDto> RecentlyOpened, IReadOnlyList<BookListItemDto> InProgress);

    /// <summary>UC-5: book lists of the reading room: recently opened and in progress.</summary>
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

            return new ReadingRoomDto(recentlyOpened, inProgress);
        }
    }
}
