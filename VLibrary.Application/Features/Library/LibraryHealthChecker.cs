using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;

namespace VLibrary.Application.Features.Library
{
    public sealed record LibraryCheckResultDto(int FilesOk, int FilesMissing);

    /// <summary>Checks that every library file still has its object in VStorage.</summary>
    public sealed record CheckLibraryCommand : IRequest<Result<LibraryCheckResultDto>>;

    /// <summary>
    /// Lists the user's VStorage bucket once and marks every file Ok or Missing.
    /// Books whose primary file is gone get another available file as primary.
    /// </summary>
    internal sealed class CheckLibraryHandler(IAppDbContext db, IBookStorage storage, IClock clock, ILogger<CheckLibraryHandler> logger)
        : IRequestHandler<CheckLibraryCommand, Result<LibraryCheckResultDto>>
    {
        private const int BatchSize = 500;

        public async Task<Result<LibraryCheckResultDto>> Handle(CheckLibraryCommand request, CancellationToken cancellationToken)
        {
            var listed = await storage.ListObjectIdsAsync(cancellationToken);
            if (listed.IsFailure)
            {
                return listed.Error;
            }

            var objectIds = listed.Value;
            int ok = 0, missing = 0;
            var touchedBooks = new HashSet<long>();
            long lastId = 0;
            while (true)
            {
                var files = await db.FileMetas
                    .Where(f => f.Id > lastId)
                    .OrderBy(f => f.Id)
                    .Take(BatchSize)
                    .ToListAsync(cancellationToken);
                if (files.Count == 0)
                {
                    break;
                }

                foreach (var file in files)
                {
                    var before = file.Status;
                    if (objectIds.Contains(file.StorageObjectId))
                    {
                        file.MarkSeen(clock.UtcNow);
                        ok++;
                    }
                    else
                    {
                        file.MarkMissing();
                        missing++;
                    }

                    if (before != file.Status && file.BookId.HasValue)
                    {
                        touchedBooks.Add(file.BookId.Value);
                    }
                }

                lastId = files[^1].Id;
                await db.SaveChangesAsync(cancellationToken);
                db.ClearChangeTracker();
            }

            await RefreshPrimaryFilesAsync(touchedBooks, cancellationToken);

            var result = new LibraryCheckResultDto(ok, missing);
            logger.LogInformation("Library check: {@Result}", result);
            return result;
        }

        private async Task RefreshPrimaryFilesAsync(IReadOnlyCollection<long> bookIds, CancellationToken cancellationToken)
        {
            foreach (var chunk in bookIds.Chunk(BatchSize))
            {
                var books = await db.Books
                    .Where(b => chunk.Contains(b.Id))
                    .Include(b => b.FilesMetas)
                    .ToListAsync(cancellationToken);
                foreach (var book in books)
                {
                    book.EnsurePrimary();
                }

                await db.SaveChangesAsync(cancellationToken);
                db.ClearChangeTracker();
            }
        }
    }
}
