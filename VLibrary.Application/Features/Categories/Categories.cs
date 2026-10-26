using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Categories
{
    /// <param name="Files">Library files directly in this category.</param>
    /// <param name="TotalFiles">Files in this category and all its subcategories.</param>
    /// <param name="Redirects">Old names and places that lead here ("also imported as").</param>
    public sealed record CategoryDto(
        long Id, string Name, long? ParentId, string Path, int Files, int TotalFiles, IReadOnlyList<CategoryRedirectDto> Redirects);

    public sealed record CategoryRedirectDto(long Id, string Path, CategoryRedirect State);

    /// <summary>All categories as a flat list ordered by path; the UI builds the tree from ParentId.</summary>
    public sealed record GetCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;

    internal sealed class GetCategoriesHandler(IAppDbContext db) : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
    {
        public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
        {
            var rows = await db.Categories.AsNoTracking()
                .Select(c => new { c.Id, c.Name, c.ParentId, c.Path, c.RedirectState, c.RedirectToId, Files = c.Files.Count })
                .ToListAsync(cancellationToken);
            var categories = rows.Where(c => c.RedirectState == null).ToList();
            var redirects = rows
                .Where(c => c.RedirectToId != null)
                .ToLookup(c => c.RedirectToId!.Value, c => new CategoryRedirectDto(c.Id, c.Path, c.RedirectState!.Value));

            // Total = own files + all descendants; paths are unique, so a prefix tells the subtree.
            return categories
                .OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                .Select(c => new CategoryDto(
                    c.Id, c.Name, c.ParentId, c.Path, c.Files,
                    categories.Where(d => d.Id == c.Id || d.Path.StartsWith(c.Path + CategoryEntity.Separator, StringComparison.Ordinal))
                        .Sum(d => d.Files),
                    redirects[c.Id].OrderBy(r => r.Path, StringComparer.OrdinalIgnoreCase).ToList()))
                .ToList();
        }
    }

    /// <summary>The ids of a category and all its subcategories (by path prefix).</summary>
    internal static class CategoryTree
    {
        public static async Task<List<long>> SubtreeIdsAsync(IAppDbContext db, long categoryId, CancellationToken cancellationToken)
        {
            var path = await db.Categories.Where(c => c.Id == categoryId).Select(c => c.Path).FirstOrDefaultAsync(cancellationToken);
            if (path == null)
            {
                return [];
            }

            var prefix = path + CategoryEntity.Separator;
            return await db.Categories
                .Where(c => c.Id == categoryId || c.Path.StartsWith(prefix))
                .Select(c => c.Id)
                .ToListAsync(cancellationToken);
        }
    }

    /// <summary>
    /// Finds or creates the category chain of an uploaded file ("Library", "Math" under an optional parent).
    /// Matches names ignoring case; a renamed / moved / merged category is found by its old name too (redirect rows).
    /// New or restored categories are changed in the context, the caller saves.
    /// </summary>
    internal sealed class CategoryResolver(IAppDbContext db)
    {
        /// <summary>Old folders of deleted categories that <see cref="GetOrCreateAsync"/> brought back (for the import report).</summary>
        public List<string> Restored { get; } = [];

        /// <summary>
        /// The category path the files of these folders get: redirects are followed, folders that do not exist yet keep
        /// their names. Creates nothing (the VStorage key is built from it before the upload).
        /// </summary>
        public async Task<string> PathOfAsync(CategoryEntity? parent, IReadOnlyList<string> folders, CancellationToken cancellationToken)
        {
            var current = parent;
            for (var i = 0; i < folders.Count; i++)
            {
                var child = await FindChildAsync(current, folders[i], cancellationToken);
                if (child == null)
                {
                    var rest = string.Join(CategoryEntity.Separator, folders.Skip(i));
                    return current == null ? rest : current.Path + CategoryEntity.Separator + rest;
                }

                current = await FollowAsync(child, cancellationToken) ?? child;
            }

            return current?.Path ?? string.Empty;
        }

        public Task<CategoryEntity?> FindAsync(CategoryEntity? parent, IReadOnlyList<string> folders, CancellationToken cancellationToken) =>
            WalkAsync(parent, folders, create: false, cancellationToken);

        /// <summary>The category of the folders; <paramref name="parent"/> itself when there are no folders.</summary>
        public async Task<CategoryEntity> GetOrCreateAsync(CategoryEntity? parent, IReadOnlyList<string> folders, CancellationToken cancellationToken) =>
            (await WalkAsync(parent, folders, create: true, cancellationToken))!;

        private async Task<CategoryEntity?> WalkAsync(
            CategoryEntity? parent, IReadOnlyList<string> folders, bool create, CancellationToken cancellationToken)
        {
            var current = parent;
            foreach (var name in folders)
            {
                var child = await FindChildAsync(current, name, cancellationToken);
                if (child == null)
                {
                    if (!create)
                    {
                        return null;
                    }

                    // The names were validated by ImportPath.Parse, so creating cannot fail here.
                    var created = current == null ? CategoryEntity.CreateTopLevel(name) : current.CreateChild(name);
                    child = created.Value;
                    db.Categories.Add(child);
                }
                else if (child.IsRedirect)
                {
                    var live = await FollowAsync(child, cancellationToken);
                    if (live != null)
                    {
                        child = live;
                    }
                    else if (!create)
                    {
                        return null;
                    }
                    else
                    {
                        // Its new category was deleted: the old folder becomes a category again.
                        Restored.Add(child.Path);
                        child.Restore();
                    }
                }

                current = child;
            }

            return current;
        }

        // The live category of a redirect row; null for a dead redirect. Live rows lead to themselves.
        private async Task<CategoryEntity?> FollowAsync(CategoryEntity category, CancellationToken cancellationToken)
        {
            if (!category.IsRedirect)
            {
                return category;
            }

            return category.RedirectToId is { } id
                ? category.RedirectTo ?? await db.Categories.FirstOrDefaultAsync(c => c.Id == id, cancellationToken)
                : null;
        }

        private async Task<CategoryEntity?> FindChildAsync(CategoryEntity? parent, string name, CancellationToken cancellationToken)
        {
            // Categories created for this file are not saved yet: look in the tracked ones first.
            var local = db.Categories.Local.FirstOrDefault(c => c.HasName(name) && IsChildOf(c, parent));
            if (local != null)
            {
                return local;
            }

            if (parent is { Id: 0 })
            {
                return null;
            }

            var key = name.Trim().ToLowerInvariant();
            var parentId = parent?.Id;
            return await db.Categories.FirstOrDefaultAsync(c => c.ParentId == parentId && c.NameKey == key, cancellationToken);
        }

        private static bool IsChildOf(CategoryEntity category, CategoryEntity? parent) =>
            parent == null
                ? category.ParentId == null && category.Parent == null
                : ReferenceEquals(category.Parent, parent) || (parent.Id > 0 && category.ParentId == parent.Id);
    }
}
