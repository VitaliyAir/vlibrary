using VLibrary.Domain.Common;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Abstractions
{
    /// <summary>
    /// An uploaded file kept in a temp folder while it is imported (hash, metadata, upload to VStorage).
    /// Disposing it deletes the temp file.
    /// </summary>
    public interface IStagedFile : IAsyncDisposable
    {
        /// <summary>Temp path; the file keeps its original name, so the file-name metadata parser works as before.</summary>
        string Path { get; }

        long SizeBytes { get; }

        /// <summary>SHA-256 of the content as 64 lower-case hex chars, computed while the upload was written.</summary>
        string Sha256 { get; }

        Stream OpenRead();
    }

    public interface IUploadStaging
    {
        /// <summary>
        /// Writes <paramref name="content"/> to a temp file named <paramref name="fileName"/> while hashing it.
        /// Fails with a validation error when the file exceeds Import:MaxFileBytes.
        /// </summary>
        Task<Result<IStagedFile>> StageAsync(Stream content, string fileName, CancellationToken cancellationToken);
    }

    public interface IBookMetadataReader
    {
        /// <summary>
        /// Reads title/authors/ISBN/... from the file itself and falls back to the file name.
        /// Never throws for unreadable files: the file-name metadata is returned instead.
        /// </summary>
        Task<ExtractedMetadata> ReadAsync(string path, CancellationToken cancellationToken);
    }
}
