using Microsoft.Extensions.Options;
using UglyToad.PdfPig;
using VLibrary.Application.Features.Imports.Metadata;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Infrastructure.Metadata
{
    internal sealed class PdfMetadataExtractor(IOptions<MetadataOptions> options) : IFormatMetadataExtractor
    {
        public bool CanRead(FileTypesEnum format) => format == FileTypesEnum.PDF;

        public Task<ExtractedMetadata?> ReadAsync(string path, CancellationToken cancellationToken) =>
            Task.Run(() => Read(path, cancellationToken), cancellationToken);

        private ExtractedMetadata? Read(string path, CancellationToken cancellationToken)
        {
            using var document = PdfDocument.Open(path);
            var information = document.Information;

            return new ExtractedMetadata
            {
                Title = information.Title,
                Authors = AuthorNamesSplitter.Split(information.Author),
                PageCount = document.NumberOfPages,
                Isbn = FindIsbn(document, cancellationToken),
                Source = "pdf"
            };
        }

        /// <summary>
        /// The ISBN is printed on the imprint page: near the start in most books, at the end in many Russian ones.
        /// </summary>
        private string? FindIsbn(PdfDocument document, CancellationToken cancellationToken)
        {
            var pageCount = document.NumberOfPages;
            var fromStart = Enumerable.Range(1, Math.Min(options.Value.PdfIsbnPagesFromStart, pageCount));
            var fromEnd = Enumerable.Range(Math.Max(1, pageCount - options.Value.PdfIsbnPagesFromEnd + 1), Math.Min(options.Value.PdfIsbnPagesFromEnd, pageCount));

            foreach (var pageNumber in fromStart.Concat(fromEnd).Distinct())
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    var isbn = Isbn.FindInText(document.GetPage(pageNumber).Text);
                    if (isbn != null)
                    {
                        return isbn;
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // A broken page should not stop the import; keep looking on the other pages.
                }
            }

            return null;
        }
    }
}
