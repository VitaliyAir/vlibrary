using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;

namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// A node of the library tree, e.g. "Library" > "Math". Files belong to exactly one category; the VStorage key
    /// of a file is <see cref="Path"/> + "/" + file name. Names are compared ignoring case ("library" = "Library").
    /// Import creates categories from folders; they can be renamed, moved and merged later. The old name and place
    /// then stay as a redirect row (<see cref="RedirectState"/>), so imports from the old folders find the category.
    /// </summary>
    public class CategoryEntity : AuditableEntity
    {
        public const int MaxNameLength = 200;
        public const int MaxPathLength = 1000;
        public const char Separator = '/';

        public string Name { get; private set; } = string.Empty;

        /// <summary>Lower-case <see cref="Name"/>, unique among the children of one parent.</summary>
        public string NameKey { get; private set; } = string.Empty;

        /// <summary>Null for a top-level category.</summary>
        public long? ParentId { get; private set; }
        public virtual CategoryEntity? Parent { get; private set; }

        /// <summary>Full path from the top level with '/' separators, e.g. "Library/Math".</summary>
        public string Path { get; private set; } = string.Empty;

        /// <summary>
        /// Null for a live category. Otherwise this row is only the old name and place of a category that was renamed,
        /// moved or merged: import follows <see cref="RedirectTo"/>; the tree, the catalog and the counts skip it.
        /// </summary>
        public CategoryRedirect? RedirectState { get; private set; }

        /// <summary>The live category the old name leads to; null when that category was deleted later ("dead").</summary>
        public long? RedirectToId { get; private set; }
        public virtual CategoryEntity? RedirectTo { get; private set; }

        public bool IsRedirect => RedirectState != null;

        public virtual ICollection<CategoryEntity> Children { get; set; } = new List<CategoryEntity>();
        public virtual ICollection<FileMetaEntity> Files { get; set; } = new List<FileMetaEntity>();

        public static Result<CategoryEntity> CreateTopLevel(string name) => Create(name, null);

        public Result<CategoryEntity> CreateChild(string name) => Create(name, this);

        /// <summary>Renames and/or moves the category (null = top level). The caller updates the subcategories (<see cref="MoveUnder"/>).</summary>
        public Result Relocate(CategoryEntity? parent, string name)
        {
            var valid = ValidateName(name);
            if (valid.IsFailure)
            {
                return valid.Error;
            }

            var path = BuildPath(parent, valid.Value);
            if (path.IsFailure)
            {
                return path.Error;
            }

            Name = valid.Value;
            NameKey = ToNameKey(valid.Value);
            Parent = parent;
            ParentId = parent?.Id;
            Path = path.Value;
            return Result.Success();
        }

        /// <summary>Same name under <paramref name="parent"/>: moved in by a merge, or the parent's path changed.</summary>
        public Result MoveUnder(CategoryEntity parent) => Relocate(parent, Name);

        /// <summary>A redirect row with this category's current name and place; made before it is renamed or moved.</summary>
        public CategoryEntity LeaveRedirect(CategoryRedirect state) => new()
        {
            Name = Name,
            NameKey = NameKey,
            Parent = Parent,
            ParentId = ParentId,
            Path = Path,
            RedirectState = state,
            RedirectTo = this,
            RedirectToId = Id
        };

        /// <summary>This row now only leads to <paramref name="target"/> (a merged category, or a redirect re-pointed after a merge).</summary>
        public void PointTo(CategoryEntity target, CategoryRedirect state)
        {
            RedirectState = state;
            RedirectTo = target;
            RedirectToId = target.Id;
        }

        /// <summary>The category this redirect led to was deleted.</summary>
        public void LoseTarget()
        {
            RedirectTo = null;
            RedirectToId = null;
        }

        /// <summary>An import used the old folder of a deleted category: the row is a live category again.</summary>
        public void Restore()
        {
            RedirectState = null;
            LoseTarget();
        }

        public bool HasName(string name) => string.Equals(NameKey, ToNameKey(name), StringComparison.Ordinal);

        /// <summary>
        /// A folder name usable as a category: trimmed, not empty, no '/', '\', control characters, "." or "..".
        /// </summary>
        public static Result<string> ValidateName(string? name)
        {
            var trimmed = name?.Trim() ?? string.Empty;
            if (trimmed.Length == 0)
            {
                return Error.Validation("Category.Name", "A category name is required.");
            }

            if (trimmed.Length > MaxNameLength)
            {
                return Error.Validation("Category.Name", $"A category name must not exceed {MaxNameLength} characters.");
            }

            if (trimmed is "." or ".." || trimmed.Any(c => char.IsControl(c) || c is Separator or '\\'))
            {
                return Error.Validation("Category.Name", $"'{trimmed}' cannot be a category name.");
            }

            return trimmed;
        }

        /// <summary>The folder names of a '/' or '\' separated path ("Library/Math/" -> Library, Math).</summary>
        public static IReadOnlyList<string> SplitPath(string? path) =>
            (path ?? string.Empty).Split([Separator, '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        private static string ToNameKey(string name) => name.Trim().ToLowerInvariant();

        private static Result<CategoryEntity> Create(string name, CategoryEntity? parent)
        {
            var valid = ValidateName(name);
            if (valid.IsFailure)
            {
                return valid.Error;
            }

            var path = BuildPath(parent, valid.Value);
            if (path.IsFailure)
            {
                return path.Error;
            }

            return new CategoryEntity
            {
                Name = valid.Value,
                NameKey = ToNameKey(valid.Value),
                Parent = parent,
                ParentId = parent?.Id is > 0 ? parent.Id : null,
                Path = path.Value
            };
        }

        private static Result<string> BuildPath(CategoryEntity? parent, string name)
        {
            var path = parent == null ? name : parent.Path + Separator + name;
            if (path.Length > MaxPathLength)
            {
                return Error.Validation("Category.Path", $"A category path must not exceed {MaxPathLength} characters.");
            }

            return path;
        }
    }
}
