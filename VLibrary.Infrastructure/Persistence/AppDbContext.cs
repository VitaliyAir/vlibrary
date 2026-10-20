using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Infrastructure.Persistence
{
    public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options), IAppDbContext
    {
        public DbSet<BookEntity> Books => Set<BookEntity>();
        public DbSet<AuthorInfoEntity> Authors => Set<AuthorInfoEntity>();
        public DbSet<BookAuthorEntity> BookAuthors => Set<BookAuthorEntity>();
        public DbSet<TagEntity> Tags => Set<TagEntity>();
        public DbSet<BookClasificatorEntity> BookClasificators => Set<BookClasificatorEntity>();
        public DbSet<CategoryEntity> Categories => Set<CategoryEntity>();
        public DbSet<FileMetaEntity> FileMetas => Set<FileMetaEntity>();
        public DbSet<FileCoverEntity> FileCovers => Set<FileCoverEntity>();
        public DbSet<BookNoteEntity> BookNotes => Set<BookNoteEntity>();
        public DbSet<BookshelfEntity> Bookshelves => Set<BookshelfEntity>();
        public DbSet<BookshelfBookEntity> BookshelfBooks => Set<BookshelfBookEntity>();
        public DbSet<ImportBatchEntity> ImportBatches => Set<ImportBatchEntity>();
        public DbSet<ImportItemEntity> ImportItems => Set<ImportItemEntity>();
        public DbSet<DuplicateCandidateEntity> DuplicateCandidates => Set<DuplicateCandidateEntity>();
        public DbSet<AppSettingEntity> AppSettings => Set<AppSettingEntity>();
        public DbSet<ActionLog> AuditLogs => Set<ActionLog>();

        public void ClearChangeTracker() => ChangeTracker.Clear();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasPostgresExtension("pg_trgm");
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        }

        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
        {
            // Enums are stored as readable text.
            StoreAsString<FileTypesEnum>(configurationBuilder);
            StoreAsString<LanguagesEnum>(configurationBuilder);
            StoreAsString<ReadingStatus>(configurationBuilder);
            StoreAsString<NoteType>(configurationBuilder);
            StoreAsString<FileStatus>(configurationBuilder);
            StoreAsString<ImportStatus>(configurationBuilder);
            StoreAsString<ImportOutcome>(configurationBuilder);
            StoreAsString<DuplicateReason>(configurationBuilder);
        }

        private static void StoreAsString<TEnum>(ModelConfigurationBuilder configurationBuilder) where TEnum : struct, Enum =>
            configurationBuilder.Properties<TEnum>().HaveConversion<string>().HaveMaxLength(32);
    }
}
