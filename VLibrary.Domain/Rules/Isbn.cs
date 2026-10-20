using System.Text.RegularExpressions;

namespace VLibrary.Domain.Rules
{
    /// <summary>
    /// ISBN-10 / ISBN-13 validation. Every valid ISBN is stored as ISBN-13 without separators.
    /// </summary>
    public static partial class Isbn
    {
        /// <summary>
        /// Returns the ISBN-13 form of <paramref name="raw"/>, or null when it is not a valid ISBN.
        /// </summary>
        public static string? Normalize(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var withoutPrefix = PrefixRegex().Replace(raw, string.Empty);
            var compact = new string(withoutPrefix
                .Where(c => char.IsAsciiDigit(c) || c is 'X' or 'x')
                .Select(char.ToUpperInvariant)
                .ToArray());

            return compact.Length switch
            {
                13 when IsValidIsbn13(compact) => compact,
                10 when IsValidIsbn10(compact) => ConvertIsbn10To13(compact),
                _ => null
            };
        }

        /// <summary>
        /// Finds the first valid ISBN that follows an "ISBN" label in free text (e.g. a book's imprint page).
        /// </summary>
        public static string? FindInText(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return null;
            }

            foreach (Match match in LabeledIsbnRegex().Matches(text))
            {
                var normalized = Normalize(match.Groups["isbn"].Value);
                if (normalized != null)
                {
                    return normalized;
                }
            }

            return null;
        }

        private static bool IsValidIsbn13(string digits)
        {
            if (!digits.All(char.IsAsciiDigit))
            {
                return false;
            }

            var sum = 0;
            for (var i = 0; i < 12; i++)
            {
                sum += (digits[i] - '0') * (i % 2 == 0 ? 1 : 3);
            }

            var check = (10 - sum % 10) % 10;
            return check == digits[12] - '0';
        }

        private static bool IsValidIsbn10(string chars)
        {
            if (!chars[..9].All(char.IsAsciiDigit))
            {
                return false;
            }

            var sum = 0;
            for (var i = 0; i < 9; i++)
            {
                sum += (chars[i] - '0') * (10 - i);
            }

            var last = chars[9];
            if (last == 'X')
            {
                sum += 10;
            }
            else if (char.IsAsciiDigit(last))
            {
                sum += last - '0';
            }
            else
            {
                return false;
            }

            return sum % 11 == 0;
        }

        private static string ConvertIsbn10To13(string isbn10)
        {
            var body = "978" + isbn10[..9];
            var sum = 0;
            for (var i = 0; i < 12; i++)
            {
                sum += (body[i] - '0') * (i % 2 == 0 ? 1 : 3);
            }

            return body + (10 - sum % 10) % 10;
        }

        [GeneratedRegex(@"^\s*ISBN(?:-1[03])?\s*:?\s*", RegexOptions.IgnoreCase)]
        private static partial Regex PrefixRegex();

        [GeneratedRegex(@"ISBN(?:-1[03])?[\s:\u00A0]{0,5}(?<isbn>(?:97[89][-\s]?)?(?:\d[-\s]?){9}[\dXx])(?![\d])", RegexOptions.IgnoreCase)]
        private static partial Regex LabeledIsbnRegex();
    }
}
