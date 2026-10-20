using System.Text.RegularExpressions;

namespace VLibrary.Application.Features.Imports.Metadata
{
    /// <summary>
    /// Splits an "authors" string from file metadata or a file name into single author names.
    /// "A; B", "A and B", "A и B", "A & B", "A, B" are lists; "Tolstoy, Leo" is one "Last, First" name.
    /// </summary>
    public static partial class AuthorNamesSplitter
    {
        public static List<string> Split(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return [];
            }

            var parts = raw.Contains(';')
                ? raw.Split(';')
                : ConjunctionRegex().Split(raw);

            var names = new List<string>();
            foreach (var part in parts)
            {
                names.AddRange(SplitCommas(part));
            }

            return names
                .Select(n => WhitespaceRegex().Replace(n, " ").Trim(' ', ',', ';', '-'))
                .Where(n => n.Length > 1)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static IEnumerable<string> SplitCommas(string part)
        {
            var pieces = part.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            // "Tolstoy, Leo" / "Толстой, Л.Н." -> one person written as "Last, First"
            if (pieces.Length == 2 && !pieces[0].Contains(' ') && !pieces[1].Contains(' '))
            {
                return [part.Trim()];
            }

            return pieces;
        }

        [GeneratedRegex(@"\s+(?:and|и|&)\s+|&", RegexOptions.IgnoreCase)]
        private static partial Regex ConjunctionRegex();

        [GeneratedRegex(@"\s+")]
        private static partial Regex WhitespaceRegex();
    }
}
