using VLibrary.Application.Features.Covers;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Books
{
    /// <summary>
    /// A book file kept in VStorage; it is opened through a download link (UC-5, POST /files/{id}/opened).
    /// </summary>
    /// <param name="LibraryPath">Category path + file name, e.g. "Library/Math/Geometry.pdf".</param>
    /// <param name="IsPrimary">The file the book opens by default.</param>
    /// <param name="CoverVersion">Set when the file has a cover: GET /files/{Id}/cover?v={CoverVersion}.</param>
    public sealed record BookFileDto(
        long Id, string FileName, long CategoryId, string LibraryPath, FileTypesEnum Format, long SizeBytes, int? PageCount,
        FileStatus Status, bool IsPrimary, long? CoverVersion);

    public sealed record NamedRefDto(long Id, string Name);

    public sealed record BookListItemDto(
        long Id,
        string Title,
        string? Subtitle,
        IReadOnlyList<string> Authors,
        int? YearPublished,
        int? Rating,
        ReadingStatus ReadingStatus,
        DateTime? LastOpenedAt,
        DateTime CreatedAt,
        IReadOnlyList<string> Tags,
        IReadOnlyList<BookFileDto> Files,
        BookFilesState FilesState,
        int NotesCount,
        CoverDto? Cover);

    public sealed record BookDetailsDto(
        long Id,
        string Title,
        string? Subtitle,
        string? Description,
        string? Isbn,
        string? Edition,
        string? Publisher,
        int? YearPublished,
        int? TotalPages,
        LanguagesEnum? Language,
        string? TableOfContents,
        string? Glossary,
        int? Rating,
        ReadingStatus ReadingStatus,
        DateTime? LastOpenedAt,
        DateTime CreatedAt,
        DateTime UpdatedAt,
        NamedRefDto? Classifier,
        IReadOnlyList<NamedRefDto> Authors,
        IReadOnlyList<NamedRefDto> Tags,
        IReadOnlyList<BookFileDto> Files,
        IReadOnlyList<NamedRefDto> Shelves,
        BookFilesState FilesState,
        int NotesCount,
        CoverDto? Cover);

    /// <summary>A file row read from the database; rows come primary first, then by id.</summary>
    internal sealed record BookFileRow(
        long Id, string FileName, long CategoryId, string LibraryPath, FileTypesEnum Format, long SizeBytes, int? PageCount,
        FileStatus Status, bool IsPrimary, DateTime? CoverUpdatedAt)
    {
        public long? CoverVersion => CoverUpdatedAt is { } updatedAt ? FileCoverEntity.VersionOf(updatedAt) : null;

        public BookFileDto ToDto() =>
            new(Id, FileName, CategoryId, LibraryPath, Format, SizeBytes, PageCount, Status, IsPrimary, CoverVersion);

        public static IReadOnlyList<BookFileDto> ToDtos(IEnumerable<BookFileRow> rows) => rows.Select(r => r.ToDto()).ToList();

        public static BookFilesState StateOf(IEnumerable<BookFileRow> rows) => BookEntity.GetFilesState(rows.Select(r => r.Status));

        /// <summary>The book cover: of the primary file, else of the first file that has one.</summary>
        public static CoverDto? CoverOf(IEnumerable<BookFileRow> rows) =>
            rows.Where(r => r.CoverVersion.HasValue).Select(r => new CoverDto(r.Id, r.CoverVersion!.Value)).FirstOrDefault();
    }

    public enum BookSort
    {
        Title = 0,
        Added = 1,
        LastOpened = 2,
        Rating = 3,
        Year = 4
    }
}
