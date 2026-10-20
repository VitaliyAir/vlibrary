using Microsoft.Extensions.Logging;
using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Imports.Metadata;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Infrastructure.Metadata
{
    public sealed class MetadataOptions
    {
        public const string SectionName = "Metadata";

        /// <summary>Full path to DjVuLibre "djvused". Empty: DjVu metadata comes from the file name only.</summary>
        public string? DjvusedPath { get; set; }

        /// <summary>
        /// Full path to DjVuLibre "ddjvu" for DjVu covers. Empty: "ddjvu" next to <see cref="DjvusedPath"/>, if it exists;
        /// otherwise DjVu books get no cover.
        /// </summary>
        public string? DdjvuPath { get; set; }

        /// <summary>How many first/last PDF pages are searched for an ISBN.</summary>
        public int PdfIsbnPagesFromStart { get; set; } = 5;
        public int PdfIsbnPagesFromEnd { get; set; } = 3;
    }

    /// <summary>
    /// Reads metadata embedded in one file format.
    /// </summary>
    internal interface IFormatMetadataExtractor
    {
        bool CanRead(FileTypesEnum format);

        /// <summary>Returns null when the file has no readable metadata.</summary>
        Task<ExtractedMetadata?> ReadAsync(string path, CancellationToken cancellationToken);
    }

    internal sealed class BookMetadataReader(IEnumerable<IFormatMetadataExtractor> extractors, ILogger<BookMetadataReader> logger)
        : IBookMetadataReader
    {
        public async Task<ExtractedMetadata> ReadAsync(string path, CancellationToken cancellationToken)
        {
            var fileName = Path.GetFileNameWithoutExtension(path);
            var fromFileName = FileNameParser.Parse(fileName);

            if (!FileFormats.TryFromExtension(Path.GetExtension(path), out var format))
            {
                return fromFileName;
            }

            var extractor = extractors.FirstOrDefault(e => e.CanRead(format));
            if (extractor == null)
            {
                return fromFileName;
            }

            ExtractedMetadata? fromFile = null;
            try
            {
                fromFile = await extractor.ReadAsync(path, cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "Cannot read embedded metadata of {Path}", path);
            }

            return MetadataMerger.Merge(fromFile, fromFileName, fileName);
        }
    }
}
