using VLibrary.Domain.Enums;

namespace VLibrary.Application.Abstractions
{
    /// <summary>A cover thumbnail as JPEG bytes.</summary>
    public sealed record CoverImage(byte[] Jpeg, int Width, int Height);

    public interface ICoverRenderer
    {
        /// <summary>Formats a cover can be made of right now (DjVu only when DjVuLibre "ddjvu" is configured).</summary>
        IReadOnlyCollection<FileTypesEnum> SupportedFormats { get; }

        /// <summary>
        /// The cover of a local file (first page / embedded cover image), scaled to the thumbnail size.
        /// Null when the format has no cover or the file cannot be read; never throws for bad files.
        /// </summary>
        Task<CoverImage?> RenderAsync(string path, FileTypesEnum format, CancellationToken cancellationToken);
    }
}
