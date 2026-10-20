using VLibrary.Domain.Common;

namespace VLibrary.Application.Features.Duplicates
{
    public static class DuplicateErrors
    {
        public static Error FileNotPending(long fileMetaId) =>
            Error.NotFound("Duplicate.NotPending", $"File {fileMetaId} is not waiting for duplicate review.");

        public static Error BookNotFound(long bookId) =>
            Error.NotFound("Duplicate.BookNotFound", $"Book {bookId} was not found.");
    }
}
