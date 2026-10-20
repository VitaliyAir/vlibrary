using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using VLibrary.Domain.Entities;

namespace VLibrary.Infrastructure.Persistence.Configurations
{
    internal sealed class BookNoteConfiguration : IEntityTypeConfiguration<BookNoteEntity>
    {
        public const string SearchVector = "SearchVector";

        public void Configure(EntityTypeBuilder<BookNoteEntity> builder)
        {
            builder.ToTable("book_notes", t => t.HasCheckConstraint(
                "ck_book_notes_review_box", $"review_box BETWEEN {BookNoteEntity.MinReviewBox} AND {BookNoteEntity.MaxReviewBox}"));
            builder.HasKey(n => n.Id);
            builder.Property(n => n.Text).IsRequired();

            builder.Property<NpgsqlTsVector>(SearchVector)
                .HasComputedColumnSql("to_tsvector('russian'::regconfig, coalesce(text, ''))", stored: true);
            builder.HasIndex(SearchVector).HasMethod("GIN");

            builder.HasOne(n => n.Book)
                   .WithMany(b => b.Notes)
                   .HasForeignKey(n => n.BookId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(n => n.FileMeta)
                   .WithMany()
                   .HasForeignKey(n => n.FileMetaId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(n => n.BookId);
            builder.HasIndex(n => n.NextReviewAt);
        }
    }

    internal sealed class BookshelfConfiguration : IEntityTypeConfiguration<BookshelfEntity>
    {
        public void Configure(EntityTypeBuilder<BookshelfEntity> builder)
        {
            builder.ToTable("bookshelves");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Name).IsRequired().HasMaxLength(200);
            builder.Property(s => s.Description).HasMaxLength(1000);
        }
    }

    internal sealed class BookshelfBookConfiguration : IEntityTypeConfiguration<BookshelfBookEntity>
    {
        public void Configure(EntityTypeBuilder<BookshelfBookEntity> builder)
        {
            builder.ToTable("bookshelf_books");
            builder.HasKey(sb => new { sb.BookshelfId, sb.BookId });

            builder.HasOne(sb => sb.Bookshelf)
                   .WithMany(s => s.ShelfBooks)
                   .HasForeignKey(sb => sb.BookshelfId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(sb => sb.Book)
                   .WithMany(b => b.ShelfBooks)
                   .HasForeignKey(sb => sb.BookId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(sb => sb.BookId);
        }
    }

    internal sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSettingEntity>
    {
        public void Configure(EntityTypeBuilder<AppSettingEntity> builder)
        {
            builder.ToTable("app_settings");
            builder.HasKey(s => s.Key);
            builder.Property(s => s.Key).HasMaxLength(100);
            builder.Property(s => s.Value).IsRequired();
        }
    }

    internal sealed class ActionLogConfiguration : IEntityTypeConfiguration<ActionLog>
    {
        public void Configure(EntityTypeBuilder<ActionLog> builder)
        {
            builder.ToTable("action_logs");
            builder.HasKey(e => e.ActionLogId);

            builder.Property(e => e.ActionById).HasMaxLength(100);
            builder.Property(e => e.EntityName).HasMaxLength(200);
            builder.Property(e => e.Action).HasMaxLength(50);
            builder.Property(e => e.EntityKey).HasMaxLength(100);
            builder.Property(e => e.Changes).HasColumnType("jsonb");

            builder.HasIndex(e => e.EntityName);
            builder.HasIndex(e => e.Action);
            builder.HasIndex(e => e.Timestamp);
            builder.HasIndex(e => e.ActionById);
        }
    }
}
