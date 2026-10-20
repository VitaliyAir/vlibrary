using System.Text.RegularExpressions;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Imports.Metadata
{
    /// <summary>
    /// Fallback metadata from a file name such as "Author - Title (Year)".
    /// </summary>
    public static partial class FileNameParser
    {
        public const string SourceName = "filename";

        private const int MaxAuthorPartWords = 6;
        private static readonly string[] AuthorTitleSeparators = [" - ", " — ", " – "];

        public static ExtractedMetadata Parse(string fileNameWithoutExtension)
        {
            var name = fileNameWithoutExtension.Replace('_', ' ');
            if (!name.Contains(' ') && name.Count(c => c == '.') >= 2)
            {
                name = name.Replace('.', ' ');
            }

            int? year = null;
            var yearMatch = YearRegex().Match(name);
            if (yearMatch.Success)
            {
                year = int.Parse(yearMatch.Groups["year"].Value);

                // Only "(2009)" / "[2009]" is an annotation; a bare year ("Кунгуру 3-4_2012") stays in the title,
                // otherwise "..._2012" and "..._2013" would get the same title and look like duplicates.
                var bracketed = yearMatch.Value.TrimStart().StartsWith('(') || yearMatch.Value.TrimStart().StartsWith('[');
                if (bracketed)
                {
                    name = name.Remove(yearMatch.Index, yearMatch.Length);
                }
            }

            name = EmptyBracketsRegex().Replace(name, " ");
            name = WhitespaceRegex().Replace(name, " ").Trim(' ', ',', '.', '-');

            var metadata = new ExtractedMetadata { Year = year, Source = SourceName, Title = name };

            var (separatorIndex, separatorLength) = FindSeparator(name);
            if (separatorIndex > 0)
            {
                var authorPart = name[..separatorIndex].Trim();
                var titlePart = name[(separatorIndex + separatorLength)..].Trim();
                var authorWords = authorPart.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;

                if (titlePart.Length > 0 && authorWords <= MaxAuthorPartWords)
                {
                    metadata.Authors = AuthorNamesSplitter.Split(authorPart);
                    metadata.Title = titlePart;
                }
            }

            return metadata;
        }

        private static (int Index, int Length) FindSeparator(string name)
        {
            foreach (var separator in AuthorTitleSeparators)
            {
                var index = name.IndexOf(separator, StringComparison.Ordinal);
                if (index > 0)
                {
                    return (index, separator.Length);
                }
            }

            return (-1, 0);
        }

        // "(1999)", "[2005]" anywhere, or ", 1999" / " 1999" at the end
        [GeneratedRegex(@"[\(\[]\s*(?<year>1[5-9]\d{2}|20\d{2})\s*[\)\]]|[,\s]\s*(?<year>1[5-9]\d{2}|20\d{2})\s*$")]
        private static partial Regex YearRegex();

        [GeneratedRegex(@"[\(\[]\s*[\)\]]")]
        private static partial Regex EmptyBracketsRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
