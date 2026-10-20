using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Domain.Entities
{
    public class BookEntity : AuditableEntity
    {
        public const int MinRating = 1;
        public const int MaxRating = 10;

        private string _title = string.Empty;

        public string Title
        {
            get => _title;
            set
            {
                _title = value;
                NormalizedTitle = TextNormalizer.Normalize(value);
                TitleWordsKey = TextNormalizer.WordsKey(value);
            }
        }

        /// <summary>Title for exact duplicate matching (case, ё, punctuation ignored).</summary>
        public string NormalizedTitle { get; private set; } = string.Empty;

        /// <summary>Title words sorted alphabetically, for word-order-insensitive duplicate matching.</summary>
        public string TitleWordsKey { get; private set; } = string.Empty;
        public string? Subtitle { get; set; }
        public string? Description { get; set; }

        /// <summary>
        /// ISBN-13 without separators (see <see cref="Isbn.Normalize"/>).
        /// </summary>
        public string? ISBN { get; private set; }

        public string? Edition { get; set; }
        public string? Publisher { get; set; }
        public int? YearPublished { get; set; }
        public int? TotalPages { get; set; }
        public LanguagesEnum? Language { get; set; }
        public byte[]? CoverImage { get; set; }
        public string? CoverImageContentType { get; set; }
        public string? TableOfContents { get; set; }
        public string? Glossary { get; set; }
        public bool IsDeleted { get; set; }

        public int? Rating { get; private set; }
        public ReadingStatus ReadingStatus { get; set; } = ReadingStatus.NotStarted;
        public DateTime? LastOpenedAt { get; private set; }

        /// <summary>
        /// Author and tag names, kept in sync by <see cref="RefreshSearchText"/>; feeds the full-text index.
        /// </summary>
        public string SearchText { get; private set; } = string.Empty;

        public long? BookClasificatorId { get; set; }
        public BookClasificatorEntity? BookClasificator { get; set; }
        public ICollection<BookAuthorEntity> BookAuthors { get; set; } = new List<BookAuthorEntity>();
        public ICollection<TagEntity> Tags { get; set; } = new List<TagEntity>();
        public ICollection<FileMetaEntity> FilesMetas { get; set; } = new List<FileMetaEntity>();
        public ICollection<BookshelfBookEntity> ShelfBooks { get; set; } = new List<BookshelfBookEntity>();
        public ICollection<BookNoteEntity> Notes { get; set; } = new List<BookNoteEntity>();

        /// <summary>Availability of the book's files; requires <see cref="FilesMetas"/> to be loaded.</summary>
        public BookFilesState FilesState => GetFilesState(FilesMetas.Where(f => !f.IsDeleted).Select(f => f.Status));

        public static BookFilesState GetFilesState(IEnumerable<FileStatus> statuses)
        {
            var list = statuses.ToList();
            if (list.Count == 0)
            {
                return BookFilesState.NoFiles;
            }

            var available = list.Count(s => s == FileStatus.Ok);
            return available == list.Count ? BookFilesState.Ok
                : available == 0 ? BookFilesState.Missing
                : BookFilesState.Partial;
        }

        /// <summary>
        /// Makes sure an available file is the primary one: keeps the current primary when it can be opened,
        /// otherwise the oldest available file becomes primary. Requires <see cref="FilesMetas"/> to be loaded.
        /// </summary>
        public void EnsurePrimary()
        {
            // Detached files may still sit in the collection until EF fixes it up, hence the ownership check.
            var files = FilesMetas
                .Where(f => !f.IsDeleted && (ReferenceEquals(f.Book, this) || (Id != 0 && f.BookId == Id)))
                .OrderBy(f => f.Id)
                .ToList();
            var primary = files.FirstOrDefault(f => f.IsPrimary);
            if (primary is { IsAvailable: true })
            {
                return;
            }

            var replacement = files.FirstOrDefault(f => f.IsAvailable) ?? primary ?? files.FirstOrDefault();
            if (replacement == null)
            {
                return;
            }

            foreach (var file in files.Where(f => f != replacement))
            {
                file.ClearPrimary();
            }

            replacement.MakePrimary();
        }

        public static BookEntity CreateFromMetadata(ExtractedMetadata metadata, string fallbackTitle)
        {
            var book = new BookEntity
            {
                Title = string.IsNullOrWhiteSpace(metadata.Title) ? fallbackTitle : metadata.Title.Trim(),
                Subtitle = metadata.Subtitle,
                ISBN = Isbn.Normalize(metadata.Isbn),
                Publisher = metadata.Publisher,
                YearPublished = metadata.Year,
                TotalPages = metadata.PageCount,
                Language = ParseLanguage(metadata.Language)
            };
            return book;
        }

        public Result SetRating(int? rating)
        {
            if (rating is < MinRating or > MaxRating)
            {
                return Error.Validation("Book.Rating", $"Rating must be between {MinRating} and {MaxRating}.");
            }

            Rating = rating;
            return Result.Success();
        }

        public Result SetIsbn(string? isbn)
        {
            if (string.IsNullOrWhiteSpace(isbn))
            {
                ISBN = null;
                return Result.Success();
            }

            var normalized = Isbn.Normalize(isbn);
            if (normalized == null)
            {
                return Error.Validation("Book.Isbn", $"'{isbn}' is not a valid ISBN-10 or ISBN-13.");
            }

            ISBN = normalized;
            return Result.Success();
        }

        /// <summary>
        /// UC-5: the user opened one of the book's files.
        /// </summary>
        public void MarkOpened(DateTime utcNow)
        {
            LastOpenedAt = utcNow;
            if (ReadingStatus == ReadingStatus.NotStarted)
            {
                ReadingStatus = ReadingStatus.InProgress;
            }
        }

        public void AddAuthor(AuthorInfoEntity author)
        {
            if (BookAuthors.Any(ba => ReferenceEquals(ba.Author, author) || (author.Id != 0 && ba.AuthorId == author.Id)))
            {
                return;
            }

            BookAuthors.Add(new BookAuthorEntity { Book = this, Author = author, Order = BookAuthors.Count });
        }

        /// <summary>
        /// Replaces the author list, keeping the given order. Requires <see cref="BookAuthors"/> to be loaded.
        /// Existing links are kept (only their order changes), so the same author is never removed and re-added.
        /// </summary>
        public void SetAuthors(IReadOnlyList<AuthorInfoEntity> authors)
        {
            static bool Same(BookAuthorEntity link, AuthorInfoEntity author) =>
                ReferenceEquals(link.Author, author) || (author.Id != 0 && link.AuthorId == author.Id);

            foreach (var link in BookAuthors.Where(link => !authors.Any(a => Same(link, a))).ToList())
            {
                BookAuthors.Remove(link);
            }

            for (var order = 0; order < authors.Count; order++)
            {
                var author = authors[order];
                var link = BookAuthors.FirstOrDefault(l => Same(l, author));
                if (link == null)
                {
                    BookAuthors.Add(new BookAuthorEntity { Book = this, Author = author, Order = order });
                }
                else
                {
                    link.Order = order;
                }
            }
        }

        /// <summary>
        /// Replaces the tags. Requires <see cref="Tags"/> to be loaded.
        /// </summary>
        public void SetTags(IReadOnlyCollection<TagEntity> tags)
        {
            foreach (var tag in Tags.Where(t => !tags.Contains(t)).ToList())
            {
                Tags.Remove(tag);
            }

            foreach (var tag in tags.Where(t => !Tags.Contains(t)))
            {
                Tags.Add(tag);
            }
        }

        /// <summary>
        /// Requires <see cref="BookAuthors"/> (with authors) and <see cref="Tags"/> to be loaded.
        /// </summary>
        public void RefreshSearchText()
        {
            var authorNames = BookAuthors
                .OrderBy(ba => ba.Order)
                .Select(ba => ba.Author?.DisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name));
            var tagNames = Tags.Select(t => t.Name);

            SearchText = string.Join(' ', authorNames.Concat(tagNames));
        }

        private static LanguagesEnum? ParseLanguage(string? language)
        {
            var code = language?.Trim().ToLowerInvariant();
            if (string.IsNullOrEmpty(code))
            {
                return null;
            }

            if (code.StartsWith("ru") || code.StartsWith("рус"))
            {
                return LanguagesEnum.Russian;
            }

            if (code.StartsWith("en"))
            {
                return LanguagesEnum.English;
            }

            if (code.StartsWith("fr"))
            {
                return LanguagesEnum.French;
            }

            return null;
        }
    }
}
