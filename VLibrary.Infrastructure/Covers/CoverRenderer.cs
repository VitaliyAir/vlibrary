using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PDFtoImage;
using SkiaSharp;
using VLibrary.Application.Abstractions;
using VLibrary.Domain.Enums;
using VLibrary.Infrastructure.Metadata;

namespace VLibrary.Infrastructure.Covers
{
    public sealed class CoverOptions
    {
        public const string SectionName = "Covers";

        /// <summary>Thumbnail width in pixels.</summary>
        public int Width { get; set; } = 300;

        /// <summary>Tall pages are scaled down further so the height stays within this.</summary>
        public int MaxHeight { get; set; } = 450;

        public int JpegQuality { get; set; } = 80;
    }

    /// <summary>
    /// Cover thumbnails: PDF first page (PDFium via PDFtoImage), EPUB embedded cover image, DjVu first page (DjVuLibre
    /// "ddjvu", optional). The picture is drawn on white (no transparency in JPEG) and scaled to the thumbnail size.
    /// </summary>
    internal sealed class CoverRenderer(
        IOptions<CoverOptions> options, IOptions<MetadataOptions> metadataOptions, ILogger<CoverRenderer> logger) : ICoverRenderer
    {
        private static readonly FileTypesEnum[] Always = [FileTypesEnum.PDF, FileTypesEnum.EPUB];
        private static readonly FileTypesEnum[] Djvu = [FileTypesEnum.DJVU, FileTypesEnum.DJV];

        public IReadOnlyCollection<FileTypesEnum> SupportedFormats =>
            DjvuCover.ResolveTool(metadataOptions.Value) != null ? [.. Always, .. Djvu] : Always;

        public async Task<CoverImage?> RenderAsync(string path, FileTypesEnum format, CancellationToken cancellationToken)
        {
            var settings = options.Value;
            try
            {
                using var bitmap = format switch
                {
                    FileTypesEnum.PDF => RenderPdf(path, settings),
                    FileTypesEnum.EPUB => EpubCover.Read(path),
                    FileTypesEnum.DJVU or FileTypesEnum.DJV => await DjvuCover.RenderAsync(
                        DjvuCover.ResolveTool(metadataOptions.Value), path, cancellationToken),
                    _ => null
                };

                return bitmap == null ? null : Encode(bitmap, settings);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogDebug(ex, "No cover for {Path}", path);
                return null;
            }
        }

        private static SKBitmap? RenderPdf(string path, CoverOptions settings)
        {
            if (!(OperatingSystem.IsWindows() || OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()))
            {
                return null;
            }

            using var stream = File.OpenRead(path);
            // Only page 1 is rendered, straight at the thumbnail width.
            return Conversion.ToImage(
                stream,
                page: 0,
                leaveOpen: false,
                options: new RenderOptions(Width: settings.Width, WithAspectRatio: true, BackgroundColor: SKColors.White));
        }

        /// <summary>Scales to the thumbnail box (never up) on a white background and encodes a JPEG.</summary>
        internal static CoverImage? Encode(SKBitmap source, CoverOptions settings)
        {
            if (source.Width <= 0 || source.Height <= 0)
            {
                return null;
            }

            var scale = Math.Min(1.0, Math.Min((double)settings.Width / source.Width, (double)settings.MaxHeight / source.Height));
            var width = Math.Max(1, (int)Math.Round(source.Width * scale));
            var height = Math.Max(1, (int)Math.Round(source.Height * scale));

            using var target = new SKBitmap(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(target))
            using (var image = SKImage.FromBitmap(source))
            {
                canvas.Clear(SKColors.White);
                canvas.DrawImage(image, new SKRect(0, 0, width, height), new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear));
            }

            using var data = target.Encode(SKEncodedImageFormat.Jpeg, Math.Clamp(settings.JpegQuality, 30, 95));
            return data == null ? null : new CoverImage(data.ToArray(), width, height);
        }
    }
}
