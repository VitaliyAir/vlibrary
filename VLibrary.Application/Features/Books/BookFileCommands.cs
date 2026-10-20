using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Features.Books
{
    /// <summary>D7: open the book with this file (another copy or format) from now on.</summary>
    public sealed record SetPrimaryFileCommand(long BookId, long FileId) : IRequest<Result>;

    /// <summary>
    /// D7: keep the book without this file. The file record stays as meta info only (not linked to any book).
    /// </summary>
    public sealed record DetachFileCommand(long BookId, long FileId) : IRequest<Result>;

    internal sealed class BookFileCommandHandlers(IAppDbContext db)
        : IRequestHandler<SetPrimaryFileCommand, Result>,
          IRequestHandler<DetachFileCommand, Result>
    {
        public async Task<Result> Handle(SetPrimaryFileCommand request, CancellationToken cancellationToken)
        {
            var book = await LoadAsync(request.BookId, cancellationToken);
            var file = book?.FilesMetas.FirstOrDefault(f => f.Id == request.FileId);
            if (book == null || file == null)
            {
                return NotFound(book, request.BookId, request.FileId);
            }

            foreach (var other in book.FilesMetas.Where(f => f != file))
            {
                other.ClearPrimary();
            }

            file.MakePrimary();
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        public async Task<Result> Handle(DetachFileCommand request, CancellationToken cancellationToken)
        {
            var book = await LoadAsync(request.BookId, cancellationToken);
            var file = book?.FilesMetas.FirstOrDefault(f => f.Id == request.FileId);
            if (book == null || file == null)
            {
                return NotFound(book, request.BookId, request.FileId);
            }

            file.Detach();
            book.EnsurePrimary();
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        private Task<BookEntity?> LoadAsync(long bookId, CancellationToken cancellationToken) =>
            db.Books.Include(b => b.FilesMetas).FirstOrDefaultAsync(b => b.Id == bookId, cancellationToken);

        private static Error NotFound(BookEntity? book, long bookId, long fileId) =>
            book == null ? CommonErrors.NotFound("Book", bookId) : CommonErrors.NotFound("File", fileId);
    }
}
