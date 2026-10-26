using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NpgsqlTypes;
using VLibrary.Domain.Entities;

namespace VLibrary.Infrastructure.Persistence.Configurations
{
    internal sealed class BookEntityConfiguration : IEntityTypeConfiguration<BookEntity>
    {
        public const string SearchVector = "SearchVector";

        public void Configure(EntityTypeBuilder<BookEntity> builder)
        {
            builder.ToTable("books", t => t.HasCheckConstraint("ck_books_rating", "rating IS NULL OR rating BETWEEN 1 AND 10"));
            builder.HasKey(b => b.Id);
            builder.Ignore(b => b.FilesState);

            builder.Property(b => b.Title).IsRequired().HasMaxLength(500);
            builder.Property(b => b.NormalizedTitle).IsRequired().HasMaxLength(500);
            builder.Property(b => b.TitleWordsKey).IsRequired().HasMaxLength(500);
            builder.HasIndex(b => b.NormalizedTitle);
            builder.HasIndex(b => b.TitleWordsKey);
            builder.Property(b => b.Subtitle).HasMaxLength(500);
            builder.Property(b => b.ISBN).HasMaxLength(13);
            builder.Property(b => b.Edition).HasMaxLength(100);
            builder.Property(b => b.Publisher).HasMaxLength(250);
            builder.Property(b => b.CoverImageContentType).HasMaxLength(100);
            builder.Property(b => b.SearchText).IsRequired().HasDefaultValue(string.Empty);

            // Full-text search (UC-4): 'russian' stems Russian words and English (ASCII) words.
            builder.Property<NpgsqlTsVector>(SearchVector)
                .HasComputedColumnSql(
                    "to_tsvector('russian'::regconfig, coalesce(title, '') || ' ' || coalesce(subtitle, '') || ' ' || coalesce(isbn, '') || ' ' || coalesce(search_text, ''))",
                    stored: true);
            builder.HasIndex(SearchVector).HasMethod("GIN");

            // Мягкое удаление (Global Query Filter)
            builder.HasQueryFilter(b => !b.IsDeleted);

            builder.HasMany(b => b.Tags)
                   .WithMany(t => t.Books)
                   .UsingEntity(j => j.ToTable("book_tags"));

            // Fuzzy title matching for duplicate detection (pg_trgm similarity / % operator) and title search.
            // EF allows one index per column set, so there is no separate b-tree index on title.
            builder.HasIndex(b => b.Title, "ix_books_title_trgm").HasMethod("GIN").HasOperators("gin_trgm_ops");
            builder.HasIndex(b => b.ISBN);
            builder.HasIndex(b => b.LastOpenedAt);
        }
    }

    internal sealed class AuthorInfoConfiguration : IEntityTypeConfiguration<AuthorInfoEntity>
    {
        public void Configure(EntityTypeBuilder<AuthorInfoEntity> builder)
        {
            builder.ToTable("authors");
            builder.HasKey(a => a.Id);
            builder.Ignore(a => a.DisplayName);

            builder.Property(a => a.FirstName).HasMaxLength(100);
            builder.Property(a => a.LastName).HasMaxLength(150);
            builder.Property(a => a.MiddleName).HasMaxLength(100);
            builder.Property(a => a.NameInitials).HasMaxLength(20);
            builder.Property(a => a.NormalizedName).IsRequired().HasMaxLength(200);

            builder.HasIndex(a => a.NormalizedName);
            builder.HasIndex(a => a.LastName);
        }
    }

    internal sealed class BookAuthorConfiguration : IEntityTypeConfiguration<BookAuthorEntity>
    {
        public void Configure(EntityTypeBuilder<BookAuthorEntity> builder)
        {
            builder.ToTable("book_authors");
            builder.HasKey(ba => new { ba.BookId, ba.AuthorId });

            builder.HasOne(ba => ba.Book)
                   .WithMany(b => b.BookAuthors)
                   .HasForeignKey(ba => ba.BookId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(ba => ba.Author)
                   .WithMany(a => a.BookAuthors)
                   .HasForeignKey(ba => ba.AuthorId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(ba => ba.AuthorId);
        }
    }

    internal sealed class TagConfiguration : IEntityTypeConfiguration<TagEntity>
    {
        public void Configure(EntityTypeBuilder<TagEntity> builder)
        {
            builder.ToTable("tags");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Name).IsRequired().HasMaxLength(100);
            builder.Property(t => t.NormalizedName).IsRequired().HasMaxLength(100);
            builder.HasIndex(t => t.NormalizedName).IsUnique();
        }
    }
}
