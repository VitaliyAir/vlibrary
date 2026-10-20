using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Infrastructure.Metadata
{
    /// <summary>
    /// Reads the Dublin Core metadata of the EPUB package document (OPF).
    /// </summary>
    internal sealed partial class EpubMetadataExtractor : IFormatMetadataExtractor
    {
        private static readonly XNamespace ContainerNs = "urn:oasis:names:tc:opendocument:xmlns:container";
        private static readonly XNamespace DcNs = "http://purl.org/dc/elements/1.1/";

        public bool CanRead(FileTypesEnum format) => format == FileTypesEnum.EPUB;

        public Task<ExtractedMetadata?> ReadAsync(string path, CancellationToken cancellationToken) =>
            Task.Run(() => Read(path), cancellationToken);

        private static ExtractedMetadata? Read(string path)
        {
            using var zip = ZipFile.OpenRead(path);

            var containerEntry = zip.GetEntry("META-INF/container.xml");
            if (containerEntry == null)
            {
                return null;
            }

            XDocument container;
            using (var stream = containerEntry.Open())
            {
                container = XDocument.Load(stream);
            }

            var opfPath = container.Descendants(ContainerNs + "rootfile").FirstOrDefault()?.Attribute("full-path")?.Value;
            var opfEntry = opfPath == null ? null : zip.GetEntry(opfPath);
            if (opfEntry == null)
            {
                return null;
            }

            XDocument opf;
            using (var stream = opfEntry.Open())
            {
                opf = XDocument.Load(stream);
            }

            string? First(string name) => opf.Descendants(DcNs + name).Select(e => e.Value.Trim()).FirstOrDefault(v => v.Length > 0);

            var yearMatch = YearRegex().Match(First("date") ?? string.Empty);

            return new ExtractedMetadata
            {
                Title = First("title"),
                Authors = opf.Descendants(DcNs + "creator").Select(e => e.Value.Trim()).Where(v => v.Length > 0).Distinct().ToList(),
                Isbn = opf.Descendants(DcNs + "identifier").Select(e => Isbn.Normalize(e.Value)).FirstOrDefault(i => i != null),
                Year = yearMatch.Success ? int.Parse(yearMatch.Value) : null,
                Publisher = First("publisher"),
                Language = First("language"),
                Source = "epub"
            };
        }

        [GeneratedRegex(@"\b(1[5-9]\d{2}|20\d{2})\b")]
        private static partial Regex YearRegex();
    }
}
