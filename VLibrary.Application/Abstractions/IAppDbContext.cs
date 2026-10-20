using Microsoft.EntityFrameworkCore;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Abstractions
{
    public interface IAppDbContext
    {
        DbSet<BookEntity> Books { get; }
        DbSet<AuthorInfoEntity> Authors { get; }
        DbSet<BookAuthorEntity> BookAuthors { get; }
        DbSet<TagEntity> Tags { get; }
        DbSet<BookClasificatorEntity> BookClasificators { get; }
        DbSet<CategoryEntity> Categories { get; }
        DbSet<FileMetaEntity> FileMetas { get; }
        DbSet<FileCoverEntity> FileCovers { get; }
        DbSet<BookNoteEntity> BookNotes { get; }
        DbSet<BookshelfEntity> Bookshelves { get; }
        DbSet<BookshelfBookEntity> BookshelfBooks { get; }
        DbSet<ImportBatchEntity> ImportBatches { get; }
        DbSet<ImportItemEntity> ImportItems { get; }
        DbSet<DuplicateCandidateEntity> DuplicateCandidates { get; }
        DbSet<AppSettingEntity> AppSettings { get; }
        DbSet<ActionLog> AuditLogs { get; }

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Detaches all tracked entities (long-running imports, recovery after a failed save).
        /// </summary>
        void ClearChangeTracker();
    }
}
