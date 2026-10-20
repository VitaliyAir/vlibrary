using System.IO.Compression;
using System.Xml.Linq;
using SkiaSharp;

namespace VLibrary.Infrastructure.Covers
{
    /// <summary>
    /// The cover image stored in an EPUB: EPUB 3 manifest item with properties="cover-image", else the EPUB 2
    /// &lt;meta name="cover" content="item id"&gt;, else a manifest image whose id or href contains "cover".
    /// </summary>
    internal static class EpubCover
    {
        private static readonly XNamespace ContainerNs = "urn:oasis:names:tc:opendocument:xmlns:container";
        private static readonly XNamespace OpfNs = "http://www.idpf.org/2007/opf";

        public static SKBitmap? Read(string path)
        {
            using var zip = ZipFile.OpenRead(path);
            var opfPath = ReadXml(zip, "META-INF/container.xml")?
                .Descendants(ContainerNs + "rootfile").FirstOrDefault()?.Attribute("full-path")?.Value;
            var opf = opfPath == null ? null : ReadXml(zip, opfPath);
            if (opf == null)
            {
                return null;
            }

            var href = FindCoverHref(opf);
            if (href == null)
            {
                return null;
            }

            // Hrefs are relative to the OPF file and may be URL-encoded.
            var folder = Path.GetDirectoryName(opfPath)?.Replace('\\', '/');
            var entryName = string.IsNullOrEmpty(folder) ? href : $"{folder}/{href}";
            var entry = zip.GetEntry(Uri.UnescapeDataString(entryName)) ?? zip.GetEntry(entryName);
            if (entry == null)
            {
                return null;
            }

            using var stream = entry.Open();
            using var buffer = new MemoryStream();
            stream.CopyTo(buffer);
            buffer.Position = 0;
            return SKBitmap.Decode(buffer);
        }

        internal static string? FindCoverHref(XDocument opf)
        {
            var items = opf.Descendants(OpfNs + "item")
                .Select(i => new
                {
                    Id = (string?)i.Attribute("id") ?? string.Empty,
                    Href = (string?)i.Attribute("href") ?? string.Empty,
                    MediaType = (string?)i.Attribute("media-type") ?? string.Empty,
                    Properties = (string?)i.Attribute("properties") ?? string.Empty
                })
                .Where(i => i.Href.Length > 0)
                .ToList();
            var images = items.Where(i => i.MediaType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)).ToList();

            var epub3 = images.FirstOrDefault(i => i.Properties.Split(' ').Contains("cover-image"));
            if (epub3 != null)
            {
                return epub3.Href;
            }

            var coverId = opf.Descendants(OpfNs + "meta")
                .Where(m => string.Equals((string?)m.Attribute("name"), "cover", StringComparison.OrdinalIgnoreCase))
                .Select(m => (string?)m.Attribute("content"))
                .FirstOrDefault();
            var epub2 = coverId == null ? null : images.FirstOrDefault(i => i.Id == coverId);
            if (epub2 != null)
            {
                return epub2.Href;
            }

            return images.FirstOrDefault(i =>
                i.Id.Contains("cover", StringComparison.OrdinalIgnoreCase) || i.Href.Contains("cover", StringComparison.OrdinalIgnoreCase))?.Href;
        }

        private static XDocument? ReadXml(ZipArchive zip, string name)
        {
            var entry = zip.GetEntry(name);
            if (entry == null)
            {
                return null;
            }

            using var stream = entry.Open();
            return XDocument.Load(stream);
        }
    }
}
