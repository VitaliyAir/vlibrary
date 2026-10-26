using VLibrary.Domain.Common;

namespace VLibrary.Application.Abstractions
{
    /// <param name="Created">False when VStorage already had the same content at this key (the upload was a no-op).</param>
    public sealed record StoredObjectInfo(Guid ObjectId, string Key, bool Created);

    public sealed record StorageDownloadLink(string Url, DateTimeOffset ExpiresAt);

    public sealed record StorageRename(Guid ObjectId, string Key);

    /// <param name="Missing">Objects VStorage no longer has; they were skipped.</param>
    public sealed record StorageRenameResult(int Renamed, IReadOnlyList<Guid> Missing);

    /// <summary>
    /// The book files in VStorage (bucket of the current user, private objects). Calls are made with the user's token,
    /// so they work only inside a user request.
    /// </summary>
    public interface IBookStorage
    {
        /// <summary>
        /// Streams a file to <paramref name="key"/>. The same content at the same key succeeds without a second copy;
        /// other content at the key fails with <see cref="StorageErrors.KeyTaken"/>.
        /// </summary>
        Task<Result<StoredObjectInfo>> UploadAsync(
            string key, string fileName, Stream content, long length, CancellationToken cancellationToken);

        /// <summary>
        /// Streams the content of an object (cover generation for files imported earlier). The caller disposes the stream.
        /// </summary>
        Task<Result<Stream>> OpenReadAsync(Guid objectId, CancellationToken cancellationToken);

        /// <summary>Deletes an object; an object that is already gone counts as deleted.</summary>
        Task<Result> DeleteAsync(Guid objectId, CancellationToken cancellationToken);

        /// <summary>
        /// New keys for objects (a category was renamed or moved). Each call to VStorage is all or nothing; a repeated
        /// call is harmless. A key used by another object fails with <see cref="StorageErrors.RenameConflict"/>.
        /// </summary>
        Task<Result<StorageRenameResult>> RenameAsync(IReadOnlyList<StorageRename> renames, CancellationToken cancellationToken);

        /// <summary>A short-lived link the browser opens directly in VStorage (inline for PDF and other safe types).</summary>
        Task<Result<StorageDownloadLink>> CreateDownloadLinkAsync(Guid objectId, CancellationToken cancellationToken);

        /// <summary>Ids of all objects in the library bucket (library check).</summary>
        Task<Result<IReadOnlySet<Guid>>> ListObjectIdsAsync(CancellationToken cancellationToken);
    }

    public static class StorageErrors
    {
        public static readonly Error Unavailable = Error.Unavailable(
            "Storage.Unavailable", "VStorage is not reachable or not configured. See the API log for details.");

        public static readonly Error NoUserToken = Error.Unavailable(
            "Storage.NoUserToken", "VStorage can only be used inside a signed-in user request.");

        public static readonly Error Forbidden = Error.Unavailable(
            "Storage.Forbidden",
            "VStorage refused: the library bucket cannot be created (your user needs the client role 'bucket-creator' of client 'vstorage').");

        public static readonly Error KeyTaken = Error.Conflict(
            "Storage.KeyTaken", "VStorage already has another file with this name in this category.");

        public static readonly Error RenameConflict = Error.Conflict(
            "Storage.RenameConflict", "VStorage already has other files at some of the new places. Nothing was changed.");

        public static readonly Error TooLarge =Error.Validation("Storage.TooLarge", "The file is too large for VStorage.");

        public static readonly Error NotFound = Error.NotFound("Storage.NotFound", "The file is no longer in VStorage.");
    }
}
