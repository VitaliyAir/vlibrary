using VLibrary.Domain.Rules;

namespace VLibrary.Domain.ValueObjects
{
    /// <summary>
    /// A parsed author name. Understands "First Last", "Last, First", "Last F.M.", "F. M. Last"
    /// and Russian "Фамилия Имя Отчество".
    /// </summary>
    public sealed record PersonName(string? FirstName, string? MiddleName, string? LastName, string? Initials)
    {
        private static readonly string[] PatronymicSuffixes = ["вич", "вна", "ична", "чна", "ич"];

        public static PersonName? Parse(string? fullName)
        {
            if (string.IsNullOrWhiteSpace(fullName))
            {
                return null;
            }

            var commaIndex = fullName.IndexOf(',');
            if (commaIndex > 0)
            {
                var last = fullName[..commaIndex].Trim();
                var restTokens = Tokenize(fullName[(commaIndex + 1)..]);
                return FromLastAndRest(last, restTokens);
            }

            var tokens = Tokenize(fullName);
            if (tokens.Count == 0)
            {
                return null;
            }

            if (tokens.Count == 1)
            {
                return new PersonName(null, null, tokens[0], null);
            }

            var initials = tokens.Where(IsInitial).ToList();
            if (initials.Count > 0)
            {
                var names = tokens.Where(t => !IsInitial(t)).ToList();
                return new PersonName(null, null, names.Count > 0 ? string.Join(' ', names) : null, JoinInitials(initials));
            }

            if (tokens.Count == 3 && IsPatronymic(tokens[2]))
            {
                return new PersonName(tokens[1], tokens[2], tokens[0], null);
            }

            var middle = tokens.Count > 2 ? string.Join(' ', tokens.Skip(1).Take(tokens.Count - 2)) : null;
            return new PersonName(tokens[0], middle, tokens[^1], null);
        }

        /// <summary>"Толстой Лев Николаевич", "Толстой Л.Н." or "Tolstoy".</summary>
        public string DisplayName => Format(LastName, FirstName, MiddleName, Initials);

        /// <summary>
        /// Key used to find the same author again: normalized last name + first initial
        /// ("Толстой Л.Н." and "Толстой Лев" both give "толстой л").
        /// </summary>
        public string MatchKey => BuildMatchKey(LastName, FirstName, Initials);

        public string NormalizedLastName => TextNormalizer.Normalize(LastName);

        public static string Format(string? lastName, string? firstName, string? middleName, string? initials)
        {
            var parts = new[] { lastName, firstName, middleName, firstName == null ? initials : null }
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join(' ', parts);
        }

        public static string BuildMatchKey(string? lastName, string? firstName, string? initials)
        {
            var initialSource = !string.IsNullOrWhiteSpace(firstName) ? firstName : initials;
            var firstInitial = TextNormalizer.Normalize(initialSource);
            var key = TextNormalizer.Normalize(lastName);
            return firstInitial.Length > 0 ? $"{key} {firstInitial[0]}".Trim() : key;
        }

        private static PersonName FromLastAndRest(string last, List<string> restTokens)
        {
            if (restTokens.Count == 0)
            {
                return new PersonName(null, null, last, null);
            }

            if (restTokens.All(IsInitial))
            {
                return new PersonName(null, null, last, JoinInitials(restTokens));
            }

            var middle = restTokens.Count > 1 ? string.Join(' ', restTokens.Skip(1)) : null;
            return new PersonName(restTokens[0], middle, last, null);
        }

        private static List<string> Tokenize(string text)
        {
            // "Л.Н.Толстой" -> "Л.", "Н.", "Толстой"
            var spaced = text.Replace(".", ". ");
            return spaced
                .Split([' ', '\t', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .ToList();
        }

        private static bool IsInitial(string token) =>
            (token.Length == 2 && char.IsLetter(token[0]) && token[1] == '.')
            || (token.Length == 1 && char.IsUpper(token[0]));

        private static string JoinInitials(IEnumerable<string> initials) =>
            string.Concat(initials.Select(i => char.ToUpperInvariant(i[0]) + "."));

        private static bool IsPatronymic(string token)
        {
            var lower = token.ToLowerInvariant();
            return PatronymicSuffixes.Any(lower.EndsWith);
        }
    }
}
