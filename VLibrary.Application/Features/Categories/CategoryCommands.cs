using System.Text;
using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Common;
using VLibrary.Application.Features.Imports;
using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Application.Features.Categories
{
    /// <param name="FilesMoved">Files whose VStorage key changed.</param>
    /// <param name="Merged">The category was joined with an existing one of the same name.</param>
    /// <param name="MissingInStorage">Files VStorage no longer has (marked missing).</param>
    public sealed record CategoryChangeDto(long CategoryId, string Path, int FilesMoved, bool Merged, int MissingInStorage);

    public sealed record CreateCategoryCommand(string Name, long? ParentId) : IRequest<Result<long>>;

    /// <summary>
    /// Rename and/or move a category with all subcategories and files (ParentId null = top level). A live category with
    /// the same name at the target is merged. The old name and place stay as a redirect row; VStorage keys follow.
    /// </summary>
    public sealed record UpdateCategoryCommand(long Id, string Name, long? ParentId) : IRequest<Result<CategoryChangeDto>>;

    /// <summary>A redirect row (an old name), or a live category without live subcategories and file records.</summary>
    public sealed record DeleteCategoryCommand(long Id) : IRequest<Result>;

    internal sealed class CreateCategoryValidator : AbstractValidator<CreateCategoryCommand>
    {
        public CreateCategoryValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(CategoryEntity.MaxNameLength);
    }

    internal sealed class UpdateCategoryValidator : AbstractValidator<UpdateCategoryCommand>
    {
        public UpdateCategoryValidator() => RuleFor(c => c.Name).NotEmpty().MaximumLength(CategoryEntity.MaxNameLength);
    }

    internal static class CategoryErrors
    {
        public static readonly Error ImportRunning = Error.Conflict(
            "Category.ImportRunning", "An import is running. Wait until it finishes (or cancel it on the Import page), then change categories.");

        public static readonly Error IntoItself = Error.Validation(
            "Category.Parent", "A category cannot be moved into itself or one of its subcategories.");

        public static readonly Error KeyTooLong = Error.Validation(
            "Category.Path", $"Some files would get a VStorage key longer than {ImportFileProcessor.MaxKeyBytes} bytes. Use a shorter name.");

        public static Error Exists(string path) => Error.Conflict("Category.Exists", $"The category '{path}' already exists.");

        public static Error FileClash(IReadOnlyList<string> paths) => Error.Conflict(
            "Category.FileClash",
            $"Merging would put two files with the same name into one category: {string.Join(", ", paths.Take(5))}" +
            $"{(paths.Count > 5 ? $" and {paths.Count - 5} more" : "")}. Rename or remove one of them first. Nothing was changed.");
    }

    internal sealed class CategoryCommandHandlers(IAppDbContext db, IBookStorage storage)
        : IRequestHandler<CreateCategoryCommand, Result<long>>,
          IRequestHandler<UpdateCategoryCommand, Result<CategoryChangeDto>>,
          IRequestHandler<DeleteCategoryCommand, Result>
    {
        public async Task<Result<long>> Handle(CreateCategoryCommand request, CancellationToken cancellationToken)
        {
            CategoryEntity? parent = null;
            if (request.ParentId is { } parentId)
            {
                parent = await db.Categories.FirstOrDefaultAsync(c => c.Id == parentId && c.RedirectState == null, cancellationToken);
                if (parent == null)
                {
                    return CommonErrors.NotFound("Category", parentId);
                }
            }

            var created = parent == null ? CategoryEntity.CreateTopLevel(request.Name) : parent.CreateChild(request.Name);
            if (created.IsFailure)
            {
                return created.Error;
            }

            var category = created.Value;
            var holder = await db.Categories.FirstOrDefaultAsync(
                c => c.ParentId == request.ParentId && c.NameKey == category.NameKey, cancellationToken);
            if (holder is { IsRedirect: false })
            {
                return CategoryErrors.Exists(category.Path);
            }

            if (holder != null)
            {
                // Only an old name of another category: the real category takes it.
                db.Categories.Remove(holder);
            }

            db.Categories.Add(category);
            await db.SaveChangesAsync(cancellationToken);
            return category.Id;
        }

        public async Task<Result<CategoryChangeDto>> Handle(UpdateCategoryCommand request, CancellationToken cancellationToken)
        {
            // Import looks categories up by name while it uploads; changing the tree meanwhile would split its files.
            if (await db.ImportBatches.AnyAsync(b => b.Status == ImportStatus.Running, cancellationToken))
            {
                return CategoryErrors.ImportRunning;
            }

            // The tree is small: it is loaded whole and tracked, so whole subtrees can be re-pathed in memory.
            var all = await db.Categories.ToListAsync(cancellationToken);
            var category = all.FirstOrDefault(c => c.Id == request.Id && !c.IsRedirect);
            if (category == null)
            {
                return CommonErrors.NotFound("Category", request.Id);
            }

            CategoryEntity? parent = null;
            if (request.ParentId is { } parentId)
            {
                parent = all.FirstOrDefault(c => c.Id == parentId && !c.IsRedirect);
                if (parent == null)
                {
                    return CommonErrors.NotFound("Category", parentId);
                }

                if (InSubtree(parent, category))
                {
                    return CategoryErrors.IntoItself;
                }
            }

            var name = CategoryEntity.ValidateName(request.Name);
            if (name.IsFailure)
            {
                return name.Error;
            }

            var moved = category.ParentId != parent?.Id;
            var renamed = !category.HasName(name.Value);
            if (!moved && category.Name == name.Value)
            {
                return new CategoryChangeDto(category.Id, category.Path, 0, false, 0);
            }

            var target = all.FirstOrDefault(c => !c.IsRedirect && c.Id != category.Id && c.ParentId == parent?.Id && c.HasName(name.Value));
            var movingIds = all.Where(c => InSubtree(c, category)).Select(c => c.Id).ToList();
            var files = await db.FileMetas.IgnoreQueryFilters()
                .Where(f => movingIds.Contains(f.CategoryId))
                .ToListAsync(cancellationToken);

            var editor = new TreeEditor(all, files);
            if (target == null)
            {
                if (moved || renamed)
                {
                    // The old name and place stay as a redirect row, so imports from the old folders find the category.
                    var claimed = all.FirstOrDefault(c => c.IsRedirect && c.ParentId == parent?.Id && c.HasName(name.Value));
                    if (claimed != null)
                    {
                        editor.Dropped.Add(claimed);
                    }

                    db.Categories.Add(category.LeaveRedirect(moved ? CategoryRedirect.Moved : CategoryRedirect.Renamed));
                }

                var relocated = category.Relocate(parent, name.Value);
                if (relocated.IsFailure)
                {
                    return relocated.Error;
                }

                var repathed = editor.RepathChildren(category);
                if (repathed.IsFailure)
                {
                    return repathed.Error;
                }
            }
            else
            {
                var targetIds = all.Where(c => InSubtree(c, target)).Select(c => c.Id).ToList();
                var existing = await db.FileMetas.IgnoreQueryFilters()
                    .Where(f => targetIds.Contains(f.CategoryId))
                    .Select(f => new { f.CategoryId, f.FileName })
                    .ToListAsync(cancellationToken);
                editor.ExistingNames.UnionWith(existing.Select(f => (f.CategoryId, f.FileName)));

                var merged = editor.Merge(category, target);
                if (merged.IsFailure)
                {
                    return merged.Error;
                }

                if (editor.Clashes.Count > 0)
                {
                    return CategoryErrors.FileClash(editor.Clashes);
                }
            }

            foreach (var dropped in editor.Dropped)
            {
                db.Categories.Remove(dropped);
            }

            var changed = editor.PlaceFiles();
            if (changed.Any(f => Encoding.UTF8.GetByteCount(f.StorageKey) > ImportFileProcessor.MaxKeyBytes))
            {
                return CategoryErrors.KeyTooLong;
            }

            var missingInStorage = 0;
            if (changed.Count > 0)
            {
                var renames = changed.Select(f => new StorageRename(f.StorageObjectId, f.StorageKey)).ToList();
                var result = await storage.RenameAsync(renames, cancellationToken);
                if (result.IsFailure)
                {
                    return result.Error;
                }

                var missing = result.Value.Missing.ToHashSet();
                foreach (var file in changed.Where(f => missing.Contains(f.StorageObjectId)))
                {
                    file.MarkMissing();
                    missingInStorage += file.IsDeleted ? 0 : 1;
                }
            }

            await db.SaveChangesAsync(cancellationToken);
            var final = target ?? category;
            return new CategoryChangeDto(final.Id, final.Path, changed.Count, target != null, missingInStorage);
        }

        public async Task<Result> Handle(DeleteCategoryCommand request, CancellationToken cancellationToken)
        {
            var category = await db.Categories.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (category == null)
            {
                return CommonErrors.NotFound("Category", request.Id);
            }

            if (!category.IsRedirect)
            {
                if (await db.Categories.AnyAsync(c => c.ParentId == request.Id && c.RedirectState == null, cancellationToken))
                {
                    return CommonErrors.InUse("Category", request.Id, "it has subcategories");
                }

                var files = await db.FileMetas.IgnoreQueryFilters().CountAsync(f => f.CategoryId == request.Id, cancellationToken);
                if (files > 0)
                {
                    return CommonErrors.InUse("Category", request.Id, $"it holds {files} file record(s) (removed files are kept for duplicate detection)");
                }

                // Old names below it go with it; old names that led to it lead nowhere now: an import from such a folder
                // creates the category again and says so.
                var related = await db.Categories
                    .Where(c => c.ParentId == request.Id || c.RedirectToId == request.Id)
                    .ToListAsync(cancellationToken);
                foreach (var row in related)
                {
                    if (row.ParentId == request.Id)
                    {
                        db.Categories.Remove(row);
                    }
                    else
                    {
                        row.LoseTarget();
                    }
                }
            }

            db.Categories.Remove(category);
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }

        // The category itself or one of its descendants (paths are unique, so a prefix tells the subtree).
        private static bool InSubtree(CategoryEntity candidate, CategoryEntity root) =>
            candidate.Id == root.Id || candidate.Path.StartsWith(root.Path + CategoryEntity.Separator, StringComparison.Ordinal);

        /// <summary>In-memory changes of the loaded tree: re-pathing, merging, and the files' new categories and keys.</summary>
        private sealed class TreeEditor(List<CategoryEntity> all, List<FileMetaEntity> files)
        {
            private readonly Dictionary<FileMetaEntity, CategoryEntity> _moves = new();

            /// <summary>(CategoryId, FileName) of the files already in the merge target's subtree.</summary>
            public HashSet<(long CategoryId, string FileName)> ExistingNames { get; } = [];

            public List<string> Clashes { get; } = [];

            /// <summary>Redirect rows to remove: their name is taken by a real category.</summary>
            public List<CategoryEntity> Dropped { get; } = [];

            /// <summary>Re-paths all rows below <paramref name="category"/>, redirect rows included.</summary>
            public Result RepathChildren(CategoryEntity category)
            {
                foreach (var child in Children(category))
                {
                    var moved = child.MoveUnder(category);
                    if (moved.IsFailure)
                    {
                        return moved;
                    }

                    var deeper = RepathChildren(child);
                    if (deeper.IsFailure)
                    {
                        return deeper;
                    }
                }

                return Result.Success();
            }

            /// <summary>
            /// <paramref name="source"/> joins <paramref name="target"/>: files and subcategories move in, same-named live
            /// ones merge again; the source stays as a redirect row to the target, and redirects to it are re-pointed.
            /// </summary>
            public Result Merge(CategoryEntity source, CategoryEntity target)
            {
                foreach (var file in files.Where(f => f.CategoryId == source.Id))
                {
                    if (ExistingNames.Contains((target.Id, file.FileName)))
                    {
                        Clashes.Add(target.Path + CategoryEntity.Separator + file.FileName);
                    }
                    else
                    {
                        _moves[file] = target;
                    }
                }

                foreach (var child in Children(source))
                {
                    var same = Children(target).FirstOrDefault(c => c.HasName(child.Name));
                    Result result;
                    if (same == null)
                    {
                        result = MoveInto(child, target);
                    }
                    else if (child.IsRedirect)
                    {
                        // The target already has this name (live or old): this old name is not needed.
                        Dropped.Add(child);
                        continue;
                    }
                    else if (same.IsRedirect)
                    {
                        // A real category takes the old name.
                        Dropped.Add(same);
                        result = MoveInto(child, target);
                    }
                    else
                    {
                        result = Merge(child, same);
                    }

                    if (result.IsFailure)
                    {
                        return result;
                    }
                }

                foreach (var redirect in all.Where(c => c.RedirectToId == source.Id && c != source))
                {
                    redirect.PointTo(target, redirect.RedirectState!.Value);
                }

                source.PointTo(target, CategoryRedirect.Merged);
                return Result.Success();
            }

            /// <summary>Every file gets its (possibly new) category and key; returns the files whose key changed.</summary>
            public List<FileMetaEntity> PlaceFiles()
            {
                var byId = all.ToDictionary(c => c.Id);
                var changed = new List<FileMetaEntity>();
                foreach (var file in files)
                {
                    var oldKey = file.StorageKey;
                    file.PlaceIn(_moves.GetValueOrDefault(file) ?? byId[file.CategoryId]);
                    if (!string.Equals(file.StorageKey, oldKey, StringComparison.Ordinal))
                    {
                        changed.Add(file);
                    }
                }

                return changed;
            }

            private Result MoveInto(CategoryEntity child, CategoryEntity target)
            {
                var moved = child.MoveUnder(target);
                return moved.IsFailure ? moved : RepathChildren(child);
            }

            private List<CategoryEntity> Children(CategoryEntity category) =>
                all.Where(c => c.ParentId == category.Id && !Dropped.Contains(c)).ToList();
        }
    }
}
