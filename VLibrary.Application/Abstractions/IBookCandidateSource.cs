namespace VLibrary.Application.Abstractions
{
    /// <summary>What is known about an imported file for duplicate matching (keys are normalized).</summary>
    /// <param name="TitleKey">Normalized title the new book would get.</param>
    /// <param name="TitleWordsKey">The same title as sorted words (word order ignored).</param>
    /// <param name="ContentTitleKey">Words key of the title stored inside the file, null when there is none.</param>
    public sealed record DuplicateQuery(
        string? Isbn13,
        string TitleKey,
        string TitleWordsKey,
        string? ContentTitleKey,
        string Extension,
        long SizeBytes);

    /// <summary>An existing book and which exact rules it matched.</summary>
    public sealed record BookCandidate(
        long BookId,
        bool IsbnEquals,
        bool TitleEquals,
        bool TitleWordsEqual,
        bool ContentTitleEquals,
        bool SizeEquals);

    public interface IBookCandidateSource
    {
        /// <summary>
        /// Existing (not deleted) books matching at least one rule: same ISBN, same title, same title words,
        /// same title inside a file, or a file with the same extension and size.
        /// </summary>
        Task<IReadOnlyList<BookCandidate>> FindAsync(DuplicateQuery query, CancellationToken cancellationToken);
    }
}
