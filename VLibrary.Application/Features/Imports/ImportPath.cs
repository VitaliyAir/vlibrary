using VLibrary.Domain.Common;
using VLibrary.Domain.Entities;

namespace VLibrary.Application.Features.Imports
{
    /// <summary>
    /// The path of an uploaded file from the chosen category start, e.g. "Library/Math/Geometry.pdf":
    /// folders (-> categories) and the file name.
    /// </summary>
    internal sealed record ImportPath(IReadOnlyList<string> Folders, string FileName)
    {
        public const int MaxFileNameLength = 300;

        public static Result<ImportPath> Parse(string? relativePath)
        {
            var parts = CategoryEntity.SplitPath(relativePath);
            if (parts.Count == 0)
            {
                return ImportErrors.InvalidPath(relativePath, "the path is empty.");
            }

            var folders = new List<string>(parts.Count - 1);
            foreach (var folder in parts.Take(parts.Count - 1))
            {
                var name = CategoryEntity.ValidateName(folder);
                if (name.IsFailure)
                {
                    return ImportErrors.InvalidPath(relativePath, name.Error.Message);
                }

                folders.Add(name.Value);
            }

            var fileName = parts[^1];
            if (fileName.Length > MaxFileNameLength || fileName is "." or ".." || fileName.Any(char.IsControl))
            {
                return ImportErrors.InvalidPath(relativePath, $"'{fileName}' is not a valid file name.");
            }

            return new ImportPath(folders, fileName);
        }

        /// <summary>The category path of the file under <paramref name="parentPath"/> ("" when there is no category).</summary>
        public string CategoryPath(string? parentPath) =>
            string.Join(CategoryEntity.Separator, parentPath == null ? Folders : Folders.Prepend(parentPath));
    }
}
