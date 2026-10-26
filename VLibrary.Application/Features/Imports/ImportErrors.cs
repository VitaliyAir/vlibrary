using VLibrary.Domain.Common;

namespace VLibrary.Application.Features.Imports
{
    public static class ImportErrors
    {
        public static Error NotFound(long id) =>
            Error.NotFound("Import.NotFound", $"Import {id} was not found.");

        public static Error AlreadyFinished(long id) =>
            Error.Conflict("Import.AlreadyFinished", $"Import {id} has already finished.");

        public static Error CannotResume(long id) =>
            Error.Conflict("Import.CannotResume", $"Import {id} is completed or failed; start a new import instead.");

        public static Error ParentCategoryMerged(long id) =>
            Error.Conflict("Import.ParentCategoryMerged", $"The parent category of import {id} was merged into another one; start a new import.");

        public static Error ParentCategoryNotFound(long id) =>
            Error.NotFound("Import.ParentCategoryNotFound", $"Category {id} was not found.");

        public static Error InvalidPath(string? path, string reason) =>
            Error.Validation("Import.InvalidPath", $"'{path}' cannot be imported: {reason}");

        public static Error NoCategory(string path) =>
            Error.Validation("Import.NoCategory",
                $"'{path}' is not inside a folder and no parent category was chosen: every library file needs a category.");

        public static Error FileTooLarge(long maxBytes) =>
            Error.Validation("Import.FileTooLarge", $"The file is larger than the import limit of {maxBytes:N0} bytes.");
    }
}
