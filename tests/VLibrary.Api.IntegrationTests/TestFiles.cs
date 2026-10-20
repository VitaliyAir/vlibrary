using System.IO.Compression;
using SkiaSharp;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace VLibrary.Api.IntegrationTests
{
    /// <summary>
    /// Small generated book files, so the tests need no binary test data.
    /// </summary>
    internal static class TestFiles
    {
        public static string CreateTempFolder()
        {
            var path = Path.Combine(Path.GetTempPath(), "vlibrary-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }

        public static void WritePdf(string path, string title, string author, string pageText) =>
            File.WriteAllBytes(path, Pdf(title, author, pageText));

        private static int _padding;

        /// <summary>
        /// A PDF whose size differs from every other one of this run, so the "same extension + size" duplicate rule
        /// does not link unrelated test books.
        /// </summary>
        public static byte[] UniquePdf(string title) =>
            // Steps of 1000 characters are far larger than any title-length difference in the tests (titles < 100 chars).
            Pdf(title, "Test Author", "text " + new string('x', Interlocked.Increment(ref _padding) * 1000));

        public static byte[] Pdf(string title, string author, string pageText)
        {
            var builder = new PdfDocumentBuilder();
            builder.DocumentInformation.Title = title;
            builder.DocumentInformation.Author = author;

            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            page.AddText(pageText, 12, new PdfPoint(25, 700), font);

            return builder.Build();
        }

        /// <param name="coverPng">When set, stored as the EPUB 3 cover image (manifest item properties="cover-image").</param>
        public static void WriteEpub(string path, string title, string creator, string isbn, byte[]? coverPng = null)
        {
            using var zip = ZipFile.Open(path, ZipArchiveMode.Create);

            WriteEntry(zip, "mimetype", "application/epub+zip");
            WriteEntry(zip, "META-INF/container.xml", """
                <?xml version="1.0"?>
                <container version="1.0" xmlns="urn:oasis:names:tc:opendocument:xmlns:container">
                  <rootfiles><rootfile full-path="OEBPS/content.opf" media-type="application/oebps-package+xml"/></rootfiles>
                </container>
                """);
            var manifest = coverPng == null
                ? string.Empty
                : "<manifest><item id=\"cover\" href=\"images/cover.png\" media-type=\"image/png\" properties=\"cover-image\"/></manifest>";
            WriteEntry(zip, "OEBPS/content.opf", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <package xmlns="http://www.idpf.org/2007/opf" version="3.0" unique-identifier="id">
                  <metadata xmlns:dc="http://purl.org/dc/elements/1.1/">
                    <dc:title>{title}</dc:title>
                    <dc:creator>{creator}</dc:creator>
                    <dc:identifier id="id">urn:isbn:{isbn}</dc:identifier>
                    <dc:date>1869</dc:date>
                    <dc:language>en</dc:language>
                  </metadata>
                  {manifest}
                </package>
                """);
            if (coverPng != null)
            {
                using var image = zip.CreateEntry("OEBPS/images/cover.png").Open();
                image.Write(coverPng);
            }
        }

        public static byte[] Epub(string title, string creator, string isbn, byte[]? coverPng = null)
        {
            var path = Path.Combine(CreateTempFolder(), "book.epub");
            WriteEpub(path, title, creator, isbn, coverPng);
            return File.ReadAllBytes(path);
        }

        /// <summary>A plain PNG (200 x 300, one colour) to use as an EPUB cover.</summary>
        public static byte[] Png(SKColor colour)
        {
            using var bitmap = new SKBitmap(200, 300);
            bitmap.Erase(colour);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return data.ToArray();
        }

        private static void WriteEntry(ZipArchive zip, string name, string content)
        {
            using var writer = new StreamWriter(zip.CreateEntry(name).Open());
            writer.Write(content);
        }
    }
}
