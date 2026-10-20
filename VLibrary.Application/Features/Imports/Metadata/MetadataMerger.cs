using System.Text.RegularExpressions;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Imports.Metadata
{
    /// <summary>
    /// Combines metadata read from the file with the file-name fallback. Embedded PDF titles and authors
    /// are often junk ("Microsoft Word - draft.doc", "Administrator"), so those are replaced by the file-name values.
    /// </summary>
    public static partial class MetadataMerger
    {
        private static readonly string[] JunkTitleSuffixes = [".pdf", ".doc", ".docx", ".rtf", ".tex", ".dvi", ".indd", ".p65", ".qxd", ".djvu"];
        private static readonly string[] JunkTitlePrefixes = ["microsoft word - ", "microsoft powerpoint - "];
        private static readonly HashSet<string> JunkTitles = ["untitled", "title", "без названия", "document", "книга"];
        private static readonly HashSet<string> JunkAuthors =
            ["admin", "administrator", "user", "owner", "unknown", "author", "автор", "пользователь", "администратор"];

        public static ExtractedMetadata Merge(ExtractedMetadata? fromFile, ExtractedMetadata fromFileName, string fileNameWithoutExtension)
        {
            if (fromFile == null)
            {
                return fromFileName;
            }

            var titleIsUsable = !IsJunkTitle(fromFile.Title, fileNameWithoutExtension);
            var authors = fromFile.Authors.Where(a => !JunkAuthors.Contains(TextNormalizer.Normalize(a))).ToList();

            return new ExtractedMetadata
            {
                Title = titleIsUsable ? fromFile.Title!.Trim() : fromFileName.Title,
                Subtitle = titleIsUsable ? fromFile.Subtitle : null,
                Source = titleIsUsable ? fromFile.Source : fromFileName.Source,
                Authors = authors.Count > 0 ? authors : fromFileName.Authors,
                Isbn = Isbn.Normalize(fromFile.Isbn),
                Year = fromFile.Year ?? fromFileName.Year,
                Publisher = string.IsNullOrWhiteSpace(fromFile.Publisher) ? null : fromFile.Publisher.Trim(),
                Language = fromFile.Language,
                PageCount = fromFile.PageCount
            };
        }

        public static bool IsJunkTitle(string? title, string fileNameWithoutExtension)
        {
            if (string.IsNullOrWhiteSpace(title) || title.Trim().Length < 2 || !title.Any(char.IsLetter))
            {
                return true;
            }

            var lower = title.Trim().ToLowerInvariant();
            return JunkTitles.Contains(lower)
                || GenericTitleRegex().IsMatch(lower)
                || JunkTitleSuffixes.Any(lower.EndsWith)
                || JunkTitlePrefixes.Any(lower.StartsWith)
                || string.Equals(lower, fileNameWithoutExtension.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // "Untitled-2", "Document1", "Без названия 3", "Title"
        [GeneratedRegex(@"^(untitled|document|документ|без названия|title|книга)[\s\-_]*\d*$")]
        private static partial Regex GenericTitleRegex();
    }
}
