using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VLibrary.Domain.Entities;

namespace VLibrary.Infrastructure.Persistence.Configurations
{
    internal sealed class FileMetaConfiguration : IEntityTypeConfiguration<FileMetaEntity>
    {
        public void Configure(EntityTypeBuilder<FileMetaEntity> builder)
        {
            builder.ToTable("file_metas");
            builder.HasKey(f => f.Id);

            builder.Ignore(f => f.IsAvailable);

            builder.Property(f => f.FileName).IsRequired().HasMaxLength(300);
            builder.Property(f => f.Extension).IsRequired().HasMaxLength(10);
            builder.Property(f => f.FileHash).IsRequired().HasMaxLength(64).IsFixedLength();
            builder.Property(f => f.StorageKey).IsRequired().HasMaxLength(1024);
            builder.Property(f => f.OriginalFileName).HasMaxLength(300); // same as FileName
            builder.Property(f => f.ContentTitleKey).HasMaxLength(500);

            builder.OwnsOne(f => f.ExtractedMetadata, metadata => metadata.ToJson("extracted_metadata"));

            builder.HasQueryFilter(f => !f.IsDeleted);

            // One record per file name in a category (soft-deleted rows included: their VStorage object keeps the key).
            // "One primary file per book" is a domain rule: a partial unique index would break the
            // clear-old / set-new primary swap inside one SaveChanges.
            builder.HasIndex(f => new { f.CategoryId, f.FileName }).IsUnique();
            builder.HasIndex(f => f.StorageObjectId).IsUnique();
            builder.HasIndex(f => f.FileHash);
            builder.HasIndex(f => f.ContentTitleKey);
            builder.HasIndex(f => new { f.Extension, f.SizeBytes });
            builder.HasIndex(f => f.BookId);
            builder.HasIndex(f => f.CoverCheckedAt);

            builder.HasOne(f => f.Category)
                   .WithMany(c => c.Files)
                   .HasForeignKey(f => f.CategoryId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(f => f.Book)
                   .WithMany(b => b.FilesMetas)
                   .HasForeignKey(f => f.BookId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }

    internal sealed class FileCoverConfiguration : IEntityTypeConfiguration<FileCoverEntity>
    {
        public void Configure(EntityTypeBuilder<FileCoverEntity> builder)
        {
            builder.ToTable("file_covers");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.ContentType).IsRequired().HasMaxLength(50);
            builder.Property(c => c.Data).IsRequired();

            // One cover per file; it goes with the file.
            builder.HasOne(c => c.FileMeta)
                   .WithOne(f => f.Cover)
                   .HasForeignKey<FileCoverEntity>(c => c.FileMetaId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }

    internal sealed class CategoryConfiguration : IEntityTypeConfiguration<CategoryEntity>
    {
        public void Configure(EntityTypeBuilder<CategoryEntity> builder)
        {
            builder.ToTable("categories");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Name).IsRequired().HasMaxLength(CategoryEntity.MaxNameLength);
            builder.Property(c => c.NameKey).IsRequired().HasMaxLength(CategoryEntity.MaxNameLength);
            builder.Property(c => c.Path).IsRequired().HasMaxLength(CategoryEntity.MaxPathLength);

            // One "Library" per parent, ignoring case; NULLS NOT DISTINCT makes it hold for top-level categories too.
            builder.HasIndex(c => new { c.ParentId, c.NameKey }).IsUnique().AreNullsDistinct(false);
            builder.HasIndex(c => c.Path);

            builder.HasOne(c => c.Parent)
                   .WithMany(c => c.Children)
                   .HasForeignKey(c => c.ParentId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Redirect rows (old names) lead to the live category; deleting it leaves them "dead" (the handler does the same).
            builder.HasOne(c => c.RedirectTo)
                   .WithMany()
                   .HasForeignKey(c => c.RedirectToId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }

    internal sealed class DuplicateCandidateConfiguration : IEntityTypeConfiguration<DuplicateCandidateEntity>
    {
        public void Configure(EntityTypeBuilder<DuplicateCandidateEntity> builder)
        {
            builder.ToTable("duplicate_candidates");
            builder.HasKey(c => c.Id);

            builder.HasOne(c => c.FileMeta)
                   .WithMany(f => f.DuplicateCandidates)
                   .HasForeignKey(c => c.FileMetaId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(c => c.CandidateBook)
                   .WithMany()
                   .HasForeignKey(c => c.CandidateBookId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(c => new { c.FileMetaId, c.CandidateBookId }).IsUnique();
            builder.HasIndex(c => c.CandidateBookId);
        }
    }

    internal sealed class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatchEntity>
    {
        public void Configure(EntityTypeBuilder<ImportBatchEntity> builder)
        {
            builder.ToTable("import_batches");
            builder.HasKey(b => b.Id);
            builder.Ignore(b => b.IsFinished);
            builder.Property(b => b.SourceName).IsRequired().HasMaxLength(1000);
            builder.HasIndex(b => b.Status);

            builder.HasOne(b => b.ParentCategory)
                   .WithMany()
                   .HasForeignKey(b => b.ParentCategoryId)
                   .OnDelete(DeleteBehavior.SetNull);
        }
    }

    internal sealed class ImportItemConfiguration : IEntityTypeConfiguration<ImportItemEntity>
    {
        public void Configure(EntityTypeBuilder<ImportItemEntity> builder)
        {
            builder.ToTable("import_items");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Path).IsRequired().HasMaxLength(1000);
            builder.Property(i => i.Message).HasMaxLength(2000);

            builder.HasOne(i => i.ImportBatch)
                   .WithMany(b => b.Items)
                   .HasForeignKey(i => i.ImportBatchId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(i => i.FileMeta)
                   .WithMany()
                   .HasForeignKey(i => i.FileMetaId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(i => new { i.ImportBatchId, i.Outcome });
            builder.HasIndex(i => i.FileMetaId);
        }
    }
}
