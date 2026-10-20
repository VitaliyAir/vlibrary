namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// Cover thumbnail of a book file (JPEG, a few tens of KB): the first page of a PDF/DjVu or the cover image of an EPUB.
    /// A book shows the cover of its primary file.
    /// </summary>
    public class FileCoverEntity : AuditableEntity
    {
        public const string JpegContentType = "image/jpeg";

        public long FileMetaId { get; private set; }
        public virtual FileMetaEntity FileMeta { get; private set; } = null!;

        public string ContentType { get; private set; } = JpegContentType;
        public int Width { get; private set; }
        public int Height { get; private set; }
        public byte[] Data { get; private set; } = [];

        public static FileCoverEntity Create(int width, int height, byte[] jpeg)
        {
            var cover = new FileCoverEntity();
            cover.Replace(width, height, jpeg);
            return cover;
        }

        public void Replace(int width, int height, byte[] jpeg)
        {
            ContentType = JpegContentType;
            Width = width;
            Height = height;
            Data = jpeg;
        }

        /// <summary>Changes whenever the image changes: part of the cover URL, so browsers may cache it forever.</summary>
        public static long VersionOf(DateTime updatedAt) =>
            new DateTimeOffset(DateTime.SpecifyKind(updatedAt, DateTimeKind.Utc)).ToUnixTimeMilliseconds();
    }
}
