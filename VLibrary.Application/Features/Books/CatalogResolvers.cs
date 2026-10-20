using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Features.Books
{
    /// <summary>
    /// Finds authors and tags by name, creating missing ones (added to the context, the caller saves).
    /// </summary>
    public sealed class CatalogResolvers(IAppDbContext db)
    {
        public async Task<List<AuthorInfoEntity>> ResolveAuthorsAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        {
            var authors = new List<AuthorInfoEntity>();
            foreach (var raw in names)
            {
                var name = PersonName.Parse(raw);
                if (name == null || string.IsNullOrEmpty(name.MatchKey))
                {
                    continue;
                }

                var author = await FindOrCreateAuthorAsync(name, cancellationToken);
                if (!authors.Contains(author))
                {
                    authors.Add(author);
                }
            }

            return authors;
        }

        public async Task<List<TagEntity>> ResolveTagsAsync(IEnumerable<string> names, CancellationToken cancellationToken)
        {
            var tags = new List<TagEntity>();
            foreach (var raw in names.Where(n => !string.IsNullOrWhiteSpace(n)))
            {
                var key = TextNormalizer.Normalize(raw);
                if (key.Length == 0 || tags.Any(t => t.NormalizedName == key))
                {
                    continue;
                }

                var tag = db.Tags.Local.FirstOrDefault(t => t.NormalizedName == key)
                    ?? await db.Tags.FirstOrDefaultAsync(t => t.NormalizedName == key, cancellationToken);
                if (tag == null)
                {
                    tag = TagEntity.Create(raw);
                    db.Tags.Add(tag);
                }

                tags.Add(tag);
            }

            return tags;
        }

        private async Task<AuthorInfoEntity> FindOrCreateAuthorAsync(PersonName name, CancellationToken cancellationToken)
        {
            var key = name.MatchKey;
            var author = db.Authors.Local.FirstOrDefault(a => a.NormalizedName == key)
                ?? await db.Authors.FirstOrDefaultAsync(a => a.NormalizedName == key, cancellationToken);

            if (author != null)
            {
                return author;
            }

            author = AuthorInfoEntity.Create(name);
            db.Authors.Add(author);
            return author;
        }
    }
}
