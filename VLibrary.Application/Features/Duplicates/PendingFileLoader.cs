using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Features.Duplicates
{
    internal static class PendingFileLoader
    {
        /// <summary>
        /// A file waiting in the review queue: not linked to a book, not removed, with unresolved candidates.
        /// </summary>
        public static Task<FileMetaEntity?> LoadPendingAsync(this IAppDbContext db, long fileMetaId, CancellationToken cancellationToken) =>
            db.FileMetas
                .Include(f => f.DuplicateCandidates)
                .FirstOrDefaultAsync(
                    f => f.Id == fileMetaId
                        && f.BookId == null
                        && f.DuplicateCandidates.Any(c => c.ResolvedAt == null),
                    cancellationToken);

        public static void ResolveCandidates(this FileMetaEntity file, DateTime utcNow)
        {
            foreach (var candidate in file.DuplicateCandidates)
            {
                candidate.Resolve(utcNow);
            }
        }
    }
}
