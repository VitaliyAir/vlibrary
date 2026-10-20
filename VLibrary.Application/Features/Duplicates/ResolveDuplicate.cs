using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Books;
using VLibrary.Domain.Common;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Duplicates
{
    /// <summary>UC-2: the file is another copy/edition of an existing book.</summary>
    public sealed record LinkDuplicateCommand(long FileMetaId, long BookId) : IRequest<Result>;

    /// <summary>UC-2: the file is a different book; create it from the extracted metadata. Returns the new book id.</summary>
    public sealed record CreateBookFromDuplicateCommand(long FileMetaId) : IRequest<Result<long>>;

    /// <summary>UC-2: drop the file from the library (soft delete; the file stays on disk).</summary>
    public sealed record RemoveDuplicateCommand(long FileMetaId) : IRequest<Result>;

    internal sealed class LinkDuplicateHandler(IAppDbContext db, IClock clock) : IRequestHandler<LinkDuplicateCommand, Result>
    {
        public async Task<Result> Handle(LinkDuplicateCommand request, CancellationToken cancellationToken)
        {
            var file = await db.LoadPendingAsync(request.FileMetaId, cancellationToken);
            if (file == null)
            {
                return DuplicateErrors.FileNotPending(request.FileMetaId);
            }

            if (!await db.Books.AnyAsync(b => b.Id == request.BookId, cancellationToken))
            {
                return DuplicateErrors.BookNotFound(request.BookId);
            }

            file.BookId = request.BookId;
            if (!await db.FileMetas.AnyAsync(f => f.BookId == request.BookId && f.IsPrimary, cancellationToken))
            {
                file.MakePrimary();
            }

            file.ResolveCandidates(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }

    internal sealed class CreateBookFromDuplicateHandler(IAppDbContext db, BookCreator bookCreator, IClock clock)
        : IRequestHandler<CreateBookFromDuplicateCommand, Result<long>>
    {
        public async Task<Result<long>> Handle(CreateBookFromDuplicateCommand request, CancellationToken cancellationToken)
        {
            var file = await db.LoadPendingAsync(request.FileMetaId, cancellationToken);
            if (file == null)
            {
                return DuplicateErrors.FileNotPending(request.FileMetaId);
            }

            var book = await bookCreator.CreateAsync(
                file.ExtractedMetadata ?? new ExtractedMetadata(),
                Path.GetFileNameWithoutExtension(file.FileName),
                cancellationToken);

            file.Book = book;
            file.MakePrimary();
            file.ResolveCandidates(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return book.Id;
        }
    }

    internal sealed class RemoveDuplicateHandler(IAppDbContext db, IClock clock) : IRequestHandler<RemoveDuplicateCommand, Result>
    {
        public async Task<Result> Handle(RemoveDuplicateCommand request, CancellationToken cancellationToken)
        {
            var file = await db.LoadPendingAsync(request.FileMetaId, cancellationToken);
            if (file == null)
            {
                return DuplicateErrors.FileNotPending(request.FileMetaId);
            }

            file.IsDeleted = true;
            file.ResolveCandidates(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
    }
}
