using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Imports;
using VLibrary.Domain.Common;

namespace VLibrary.Infrastructure.Files
{
    public sealed class ImportOptions
    {
        public const string SectionName = "Import";

        /// <summary>Largest file one upload may carry (VStorage allows 1 GB by default).</summary>
        public long MaxFileBytes { get; set; } = 1024L * 1024 * 1024;

        /// <summary>Where uploads are kept while they are imported; empty = {system temp}\vlibrary-import.</summary>
        public string TempFolder { get; set; } = string.Empty;

        /// <summary>Folder names (case-insensitive) the UI never uploads, e.g. node_modules.</summary>
        public string[] ExcludedFolders { get; set; } = ["node_modules", ".git", ".svn", ".vs", "bin", "obj"];

        public string ResolvedTempFolder =>
            string.IsNullOrWhiteSpace(TempFolder) ? Path.Combine(Path.GetTempPath(), "vlibrary-import") : TempFolder;
    }

    /// <summary>
    /// Writes an upload to {temp}/{guid}/{file name} and hashes it on the way (one pass over the request stream).
    /// </summary>
    internal sealed class UploadStaging(IOptions<ImportOptions> options, ILogger<UploadStaging> logger) : IUploadStaging
    {
        private const int BufferSize = 1024 * 1024;

        public async Task<Result<IStagedFile>> StageAsync(Stream content, string fileName, CancellationToken cancellationToken)
        {
            var maxBytes = options.Value.MaxFileBytes;
            var folder = Path.Combine(options.Value.ResolvedTempFolder, Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
            var path = Path.Combine(folder, fileName);

            var staged = false;
            try
            {
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long size = 0;
                await using (var target = new FileStream(
                                 path, FileMode.CreateNew, FileAccess.Write, FileShare.None, BufferSize, FileOptions.Asynchronous))
                {
                    var buffer = new byte[BufferSize];
                    int read;
                    while ((read = await content.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        size += read;
                        if (size > maxBytes)
                        {
                            return ImportErrors.FileTooLarge(maxBytes);
                        }

                        hash.AppendData(buffer, 0, read);
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    }
                }

                staged = true;
                return new StagedFile(path, size, Convert.ToHexStringLower(hash.GetHashAndReset()), logger);
            }
            finally
            {
                if (!staged)
                {
                    StagedFile.DeleteFolder(folder, logger);
                }
            }
        }

        private sealed class StagedFile(string path, long sizeBytes, string sha256, ILogger logger) : IStagedFile
        {
            public string Path { get; } = path;
            public long SizeBytes { get; } = sizeBytes;
            public string Sha256 { get; } = sha256;

            public Stream OpenRead() => new FileStream(
                Path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.Asynchronous | FileOptions.SequentialScan);

            public ValueTask DisposeAsync()
            {
                DeleteFolder(System.IO.Path.GetDirectoryName(Path)!, logger);
                return ValueTask.CompletedTask;
            }

            public static void DeleteFolder(string folder, ILogger logger)
            {
                try
                {
                    Directory.Delete(folder, recursive: true);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    logger.LogWarning(ex, "Cannot delete the import temp folder {Folder}", folder);
                }
            }
        }
    }
}
