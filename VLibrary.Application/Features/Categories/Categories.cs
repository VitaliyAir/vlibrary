using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Features.Categories
{
    /// <param name="Files">Library files directly in this category.</param>
    /// <param name="TotalFiles">Files in this category and all its subcategories.</param>
    public sealed record CategoryDto(long Id, string Name, long? ParentId, string Path, int Files, int TotalFiles);

    /// <summary>All categories as a flat list ordered by path; the UI builds the tree from ParentId.</summary>
    public sealed record GetCategoriesQuery : IRequest<Result<IReadOnlyList<CategoryDto>>>;

    internal sealed class GetCategoriesHandler(IAppDbContext db) : IRequestHandler<GetCategoriesQuery, Result<IReadOnlyList<CategoryDto>>>
    {
        public async Task<Result<IReadOnlyList<CategoryDto>>> Handle(GetCategoriesQuery request, CancellationToken cancellationToken)
        {
            var categories = await db.Categories.AsNoTracking()
                .Select(c => new { c.Id, c.Name, c.ParentId, c.Path, Files = c.Files.Count })
                .ToListAsync(cancellationToken);

            // Total = own files + all descendants; paths are unique, so a prefix tells the subtree.
            return categories
                .OrderBy(c => c.Path, StringComparer.OrdinalIgnoreCase)
                .Select(c => new CategoryDto(
                    c.Id, c.Name, c.ParentId, c.Path, c.Files,
                    categories.Where(d => d.Id == c.Id || d.Path.StartsWith(c.Path + CategoryEntity.Separator, StringComparison.Ordinal))
                        .Sum(d => d.Files)))
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
    /// Matches names ignoring case; new categories are added to the context, the caller saves.
    /// </summary>
    internal sealed class CategoryResolver(IAppDbContext db)
    {
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

                current = child;
            }

            return current;
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
