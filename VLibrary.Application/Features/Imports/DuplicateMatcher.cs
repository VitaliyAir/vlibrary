using VLibrary.Application.Abstractions;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Imports
{
    public sealed record BookMatch(long BookId, double Score, DuplicateReason Reason);

    /// <summary>
    /// UC-1 / UC-2 duplicate rules (exact matches only; the best rule per book wins):
    /// same ISBN 100%, same title 100%, same title words in another order 90%, same title inside the file 90%,
    /// same extension and file size 80%. Everything else scores below 50% and is ignored.
    /// </summary>
    public static class DuplicateMatcher
    {
        public const double IsbnScore = 1.0;
        public const double TitleScore = 1.0;
        public const double TitleWordsScore = 0.9;
        public const double ContentTitleScore = 0.9;
        public const double SizeScore = 0.8;
        public const double MinScore = 0.5;
        public const int MaxMatches = 5;

        public static IReadOnlyList<BookMatch> Match(IReadOnlyList<BookCandidate> candidates) =>
            candidates
                .Select(Best)
                .OfType<BookMatch>()
                .Where(m => m.Score >= MinScore)
                .GroupBy(m => m.BookId)
                .Select(g => g.MaxBy(m => m.Score)!)
                .OrderByDescending(m => m.Score)
                .ThenBy(m => m.BookId)
                .Take(MaxMatches)
                .ToList();

        private static BookMatch? Best(BookCandidate c) =>
            c.IsbnEquals ? new BookMatch(c.BookId, IsbnScore, DuplicateReason.IsbnMatch)
            : c.TitleEquals ? new BookMatch(c.BookId, TitleScore, DuplicateReason.TitleMatch)
            : c.TitleWordsEqual ? new BookMatch(c.BookId, TitleWordsScore, DuplicateReason.TitleWordsMatch)
            : c.ContentTitleEquals ? new BookMatch(c.BookId, ContentTitleScore, DuplicateReason.ContentTitleMatch)
            : c.SizeEquals ? new BookMatch(c.BookId, SizeScore, DuplicateReason.SizeMatch)
            : null;
    }
}
