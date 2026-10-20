using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Application.Features.Books;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Features.Shelves
{
    public sealed record ShelfDto(long Id, string Name, string? Description, int SortOrder, int BooksCount);

    public sealed record ShelfDetailsDto(long Id, string Name, string? Description, int SortOrder, IReadOnlyList<BookListItemDto> Books);

    public sealed record GetShelvesQuery : IRequest<Result<IReadOnlyList<ShelfDto>>>;

    public sealed record GetShelfQuery(long Id) : IRequest<Result<ShelfDetailsDto>>;

    public sealed record CreateShelfCommand(string Name, string? Description) : IRequest<Result<long>>;

    public sealed record UpdateShelfCommand(long Id, string Name, string? Description) : IRequest<Result>;

    public sealed record DeleteShelfCommand(long Id) : IRequest<Result>;

    /// <summary>All shelf ids in the new display order.</summary>
    public sealed record ReorderShelvesCommand(IReadOnlyList<long> ShelfIds) : IRequest<Result>;

    public sealed record AddBookToShelfCommand(long ShelfId, long BookId) : IRequest<Result>;

    public sealed record RemoveBookFromShelfCommand(long ShelfId, long BookId) : IRequest<Result>;

    /// <summary>The shelf's book ids in the new order.</summary>
    public sealed record ReorderShelfBooksCommand(long ShelfId, IReadOnlyList<long> BookIds) : IRequest<Result>;

    internal sealed class CreateShelfValidator : AbstractValidator<CreateShelfCommand>
    {
        public CreateShelfValidator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Description).MaximumLength(1000);
        }
    }

    internal sealed class UpdateShelfValidator : AbstractValidator<UpdateShelfCommand>
    {
        public UpdateShelfValidator()
        {
            RuleFor(c => c.Name).NotEmpty().MaximumLength(200);
            RuleFor(c => c.Description).MaximumLength(1000);
        }
    }

    internal sealed class ShelfQueryHandlers(IAppDbContext db)
        : IRequestHandler<GetShelvesQuery, Result<IReadOnlyList<ShelfDto>>>,
          IRequestHandler<GetShelfQuery, Result<ShelfDetailsDto>>
    {
        public async Task<Result<IReadOnlyList<ShelfDto>>> Handle(GetShelvesQuery request, CancellationToken cancellationToken)
        {
            var shelves = await db.Bookshelves.AsNoTracking()
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
                .Select(s => new ShelfDto(s.Id, s.Name, s.Description, s.SortOrder, s.ShelfBooks.Count(sb => !sb.Book.IsDeleted)))
                .ToListAsync(cancellationToken);
            return shelves;
        }

        public async Task<Result<ShelfDetailsDto>> Handle(GetShelfQuery request, CancellationToken cancellationToken)
        {
            var shelf = await db.Bookshelves.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (shelf == null)
            {
                return CommonErrors.NotFound("Shelf", request.Id);
            }

            var books = await db.BookshelfBooks
                .Where(sb => sb.BookshelfId == request.Id)
                .OrderBy(sb => sb.Position).ThenBy(sb => sb.AddedAt)
                .Select(sb => sb.Book)
                .ToListItemsAsync(cancellationToken);

            return new ShelfDetailsDto(shelf.Id, shelf.Name, shelf.Description, shelf.SortOrder, books);
        }
    }

    internal sealed class ShelfCommandHandlers(IAppDbContext db, IClock clock)
        : IRequestHandler<CreateShelfCommand, Result<long>>,
          IRequestHandler<UpdateShelfCommand, Result>,
          IRequestHandler<DeleteShelfCommand, Result>,
          IRequestHandler<ReorderShelvesCommand, Result>,
          IRequestHandler<AddBookToShelfCommand, Result>,
          IRequestHandler<RemoveBookFromShelfCommand, Result>,
          IRequestHandler<ReorderShelfBooksCommand, Result>
    {
        public async Task<Result<long>> Handle(CreateShelfCommand request, CancellationToken cancellationToken)
        {
            var nextOrder = (await db.Bookshelves.MaxAsync(s => (int?)s.SortOrder, cancellationToken) ?? -1) + 1;
            var shelf = new BookshelfEntity { Name = request.Name.Trim(), Description = request.Description, SortOrder = nextOrder };
            db.Bookshelves.Add(shelf);
            await db.SaveChangesAsync(cancellationToken);
            return shelf.Id;
        }

        public async Task<Result> Handle(UpdateShelfCommand request, CancellationToken cancellationToken)
        {
            var shelf = await db.Bookshelves.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (shelf == null)
            {
                return CommonErrors.NotFound("Shelf", request.Id);
            }

            shelf.Name = request.Name.Trim();
            shelf.Description = request.Description;
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(DeleteShelfCommand request, CancellationToken cancellationToken)
        {
            var shelf = await db.Bookshelves.FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (shelf == null)
            {
                return CommonErrors.NotFound("Shelf", request.Id);
            }

            // Books stay in the library; only their shelf links are removed (cascade).
            db.Bookshelves.Remove(shelf);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(ReorderShelvesCommand request, CancellationToken cancellationToken)
        {
            var shelves = await db.Bookshelves.ToListAsync(cancellationToken);
            ApplyOrder(shelves, s => s.Id, request.ShelfIds, (s, order) => s.SortOrder = order);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(AddBookToShelfCommand request, CancellationToken cancellationToken)
        {
            if (!await db.Bookshelves.AnyAsync(s => s.Id == request.ShelfId, cancellationToken))
            {
                return CommonErrors.NotFound("Shelf", request.ShelfId);
            }

            if (!await db.Books.AnyAsync(b => b.Id == request.BookId, cancellationToken))
            {
                return CommonErrors.NotFound("Book", request.BookId);
            }

            if (await db.BookshelfBooks.AnyAsync(sb => sb.BookshelfId == request.ShelfId && sb.BookId == request.BookId, cancellationToken))
            {
                return Result.Success();
            }

            var nextPosition = (await db.BookshelfBooks
                .Where(sb => sb.BookshelfId == request.ShelfId)
                .MaxAsync(sb => (int?)sb.Position, cancellationToken) ?? -1) + 1;

            db.BookshelfBooks.Add(new BookshelfBookEntity
            {
                BookshelfId = request.ShelfId,
                BookId = request.BookId,
                Position = nextPosition,
                AddedAt = clock.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(RemoveBookFromShelfCommand request, CancellationToken cancellationToken)
        {
            var link = await db.BookshelfBooks
                .FirstOrDefaultAsync(sb => sb.BookshelfId == request.ShelfId && sb.BookId == request.BookId, cancellationToken);
            if (link != null)
            {
                db.BookshelfBooks.Remove(link);
                await db.SaveChangesAsync(cancellationToken);
            }

            return Result.Success();
        }

        public async Task<Result> Handle(ReorderShelfBooksCommand request, CancellationToken cancellationToken)
        {
            if (!await db.Bookshelves.AnyAsync(s => s.Id == request.ShelfId, cancellationToken))
            {
                return CommonErrors.NotFound("Shelf", request.ShelfId);
            }

            var links = await db.BookshelfBooks.Where(sb => sb.BookshelfId == request.ShelfId).ToListAsync(cancellationToken);
            ApplyOrder(links, sb => sb.BookId, request.BookIds, (sb, position) => sb.Position = position);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        /// <summary>
        /// Listed items get positions 0..n-1 in the given order; items missing from the list keep their relative order after them.
        /// </summary>
        private static void ApplyOrder<T>(List<T> items, Func<T, long> idOf, IReadOnlyList<long> orderedIds, Action<T, int> setOrder)
        {
            var rank = orderedIds.Select((id, index) => (id, index)).DistinctBy(x => x.id).ToDictionary(x => x.id, x => x.index);
            var ordered = items
                .OrderBy(i => rank.TryGetValue(idOf(i), out var r) ? r : int.MaxValue)
                .ToList();

            for (var position = 0; position < ordered.Count; position++)
            {
                setOrder(ordered[position], position);
            }
        }
    }
}
