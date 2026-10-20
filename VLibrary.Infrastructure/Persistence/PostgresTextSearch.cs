using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Rules;
using VLibrary.Infrastructure.Persistence.Configurations;

namespace VLibrary.Infrastructure.Persistence
{
    /// <summary>
    /// Full-text search over the generated tsvector columns ('russian' configuration) plus an ILIKE fallback
    /// on titles / note text for fragments inside words.
    /// </summary>
    internal sealed partial class PostgresTextSearch : ITextSearch
    {
        private const string Config = "russian";

        public IQueryable<BookEntity> FilterBooks(IQueryable<BookEntity> books, string text)
        {
            var tsQuery = BuildPrefixQuery(text);
            var like = "%" + EscapeLike(text) + "%";
            var isbn = Isbn.Normalize(text) ?? string.Empty;

            if (tsQuery == null)
            {
                return books.Where(b => EF.Functions.ILike(b.Title, like, "\\") || b.ISBN == isbn);
            }

            return books.Where(b =>
                EF.Property<NpgsqlTsVector>(b, BookEntityConfiguration.SearchVector).Matches(EF.Functions.ToTsQuery(Config, tsQuery))
                || EF.Functions.ILike(b.Title, like, "\\")
                || b.ISBN == isbn
                || b.Notes.Any(n => EF.Property<NpgsqlTsVector>(n, BookNoteConfiguration.SearchVector).Matches(EF.Functions.ToTsQuery(Config, tsQuery))));
        }

        public IQueryable<BookNoteEntity> FilterNotes(IQueryable<BookNoteEntity> notes, string text)
        {
            var tsQuery = BuildPrefixQuery(text);
            var like = "%" + EscapeLike(text) + "%";

            if (tsQuery == null)
            {
                return notes.Where(n => EF.Functions.ILike(n.Text, like, "\\"));
            }

            return notes.Where(n =>
                EF.Property<NpgsqlTsVector>(n, BookNoteConfiguration.SearchVector).Matches(EF.Functions.ToTsQuery(Config, tsQuery))
                || EF.Functions.ILike(n.Text, like, "\\"));
        }

        /// <summary>"войн мир" -> "войн:* &amp; мир:*". Only letters and digits get through, so the query is always valid.</summary>
        internal static string? BuildPrefixQuery(string text)
        {
            var words = WordRegex().Matches(text).Select(m => m.Value.ToLowerInvariant()).Distinct().Take(10).ToList();
            return words.Count == 0 ? null : string.Join(" & ", words.Select(w => w + ":*"));
        }

        private static string EscapeLike(string text) =>
            text.Trim().Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");

        [GeneratedRegex(@"[\p{L}\p{N}]+")]
        private static partial Regex WordRegex();
    }
}
