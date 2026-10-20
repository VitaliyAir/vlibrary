using System.Linq.Expressions;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Files
{
    /// <param name="LibraryPath">Category path + file name, e.g. "Library/Math/Geometry.pdf" (the VStorage key).</param>
    public sealed record FileListItemDto(
        long Id, string FileName, long CategoryId, string LibraryPath, FileTypesEnum Format, long SizeBytes, int? PageCount,
        FileStatus Status, long? BookId, string? BookTitle, DateTime LastSeenAt);

    /// <param name="Url">Short-lived VStorage link the browser opens directly.</param>
    public sealed record OpenedFileDto(long FileId, string Url, DateTimeOffset ExpiresAt, long? BookId);

    /// <param name="Linked">true: files of books; false: files without a book (waiting for review).</param>
    public sealed record GetFilesQuery(bool? Linked = null, FileStatus? Status = null, int Page = 1, int PageSize = 100)
        : IRequest<Result<PagedList<FileListItemDto>>>;

    public sealed record GetFileQuery(long Id) : IRequest<Result<FileListItemDto>>;

    /// <summary>
    /// UC-5: open a file. Asks VStorage for a short-lived download link and updates the book's LastOpenedAt
    /// (NotStarted -> InProgress). The content goes from VStorage straight to the browser.
    /// </summary>
    public sealed record MarkFileOpenedCommand(long Id) : IRequest<Result<OpenedFileDto>>;

    internal sealed class GetFilesValidator : AbstractValidator<GetFilesQuery>
    {
        public GetFilesValidator()
        {
            RuleFor(q => q.Page).GreaterThanOrEqualTo(1);
            RuleFor(q => q.PageSize).InclusiveBetween(1, 500);
        }
    }

    internal sealed class FileHandlers(IAppDbContext db, IBookStorage storage, IClock clock)
        : IRequestHandler<GetFilesQuery, Result<PagedList<FileListItemDto>>>,
          IRequestHandler<GetFileQuery, Result<FileListItemDto>>,
          IRequestHandler<MarkFileOpenedCommand, Result<OpenedFileDto>>
    {
        private static readonly Expression<Func<FileMetaEntity, FileListItemDto>> Projection = f => new FileListItemDto(
            f.Id, f.FileName, f.CategoryId, f.StorageKey, f.Format, f.SizeBytes, f.PageCount, f.Status,
            f.BookId, f.Book != null ? f.Book.Title : null, f.LastSeenAt);

        public async Task<Result<PagedList<FileListItemDto>>> Handle(GetFilesQuery request, CancellationToken cancellationToken)
        {
            var query = db.FileMetas.AsNoTracking();
            if (request.Linked.HasValue)
            {
                query = request.Linked.Value ? query.Where(f => f.BookId != null) : query.Where(f => f.BookId == null);
            }

            if (request.Status.HasValue)
            {
                query = query.Where(f => f.Status == request.Status.Value);
            }

            var total = await query.CountAsync(cancellationToken);
            var items = await query
                .OrderBy(f => f.StorageKey).ThenBy(f => f.Id)
                .Skip((request.Page - 1) * request.PageSize)
                .Take(request.PageSize)
                .Select(Projection)
                .ToListAsync(cancellationToken);

            return new PagedList<FileListItemDto>(items, request.Page, request.PageSize, total);
        }

        public async Task<Result<FileListItemDto>> Handle(GetFileQuery request, CancellationToken cancellationToken)
        {
            var file = await db.FileMetas.AsNoTracking()
                .Where(f => f.Id == request.Id)
                .Select(Projection)
                .FirstOrDefaultAsync(cancellationToken);

            return file == null ? CommonErrors.NotFound("File", request.Id) : file;
        }

        public async Task<Result<OpenedFileDto>> Handle(MarkFileOpenedCommand request, CancellationToken cancellationToken)
        {
            var file = await db.FileMetas.Include(f => f.Book).FirstOrDefaultAsync(f => f.Id == request.Id, cancellationToken);
            if (file == null)
            {
                return CommonErrors.NotFound("File", request.Id);
            }

            var link = await storage.CreateDownloadLinkAsync(file.StorageObjectId, cancellationToken);
            if (link.IsFailure)
            {
                if (link.Error == StorageErrors.NotFound)
                {
                    // The object is gone: shown as missing (the library check also picks another primary file).
                    file.MarkMissing();
                    await db.SaveChangesAsync(cancellationToken);
                }

                return link.Error;
            }

            file.MarkSeen(clock.UtcNow);
            file.Book?.MarkOpened(clock.UtcNow);
            await db.SaveChangesAsync(cancellationToken);

            return new OpenedFileDto(file.Id, link.Value.Url, link.Value.ExpiresAt, file.BookId);
        }
    }
}
