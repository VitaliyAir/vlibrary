using System.Text;

namespace VLibrary.Domain.Rules
{
    /// <summary>
    /// Builds comparison keys for titles, names and tags: lower case, "ё" -> "е",
    /// punctuation removed, whitespace collapsed.
    /// </summary>
    public static class TextNormalizer
    {
        public static string Normalize(string? text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            var pendingSpace = false;

            foreach (var raw in text)
            {
                var c = char.ToLowerInvariant(raw);
                if (c == 'ё')
                {
                    c = 'е';
                }

                if (char.IsLetterOrDigit(c))
                {
                    if (pendingSpace && builder.Length > 0)
                    {
                        builder.Append(' ');
                    }

                    builder.Append(c);
                    pendingSpace = false;
                }
                else
                {
                    pendingSpace = true;
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Normalized words sorted alphabetically, so word order does not matter:
        /// "Кенгуру 3-4 2012" and "кенгуру 2012 3-4" both give "2012 3 4 кенгуру".
        /// </summary>
        public static string WordsKey(string? text)
        {
            var normalized = Normalize(text);
            return normalized.Length == 0
                ? string.Empty
                : string.Join(' ', normalized.Split(' ').Order(StringComparer.Ordinal));
        }
    }
}
