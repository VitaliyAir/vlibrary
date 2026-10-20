using VLibrary.Domain.Common;

namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// A node of the library tree, e.g. "Library" > "Math". Files belong to exactly one category; the VStorage key
    /// of a file is <see cref="Path"/> + "/" + file name. Names are compared ignoring case ("library" = "Library").
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

        public virtual ICollection<CategoryEntity> Children { get; set; } = new List<CategoryEntity>();
        public virtual ICollection<FileMetaEntity> Files { get; set; } = new List<FileMetaEntity>();

        public static Result<CategoryEntity> CreateTopLevel(string name) => Create(name, null);

        public Result<CategoryEntity> CreateChild(string name) => Create(name, this);

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

            var path = parent == null ? valid.Value : parent.Path + Separator + valid.Value;
            if (path.Length > MaxPathLength)
            {
                return Error.Validation("Category.Path", $"A category path must not exceed {MaxPathLength} characters.");
            }

            return new CategoryEntity
            {
                Name = valid.Value,
                NameKey = ToNameKey(valid.Value),
                Parent = parent,
                ParentId = parent?.Id is > 0 ? parent.Id : null,
                Path = path
            };
        }
    }
}
