using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Domain.Entities
{
    /// <summary>
    /// Meta info of one book file. The content lives in VStorage (<see cref="StorageObjectId"/>, key
    /// <see cref="StorageKey"/> = category path + "/" + file name); VLibrary keeps only this record.
    /// </summary>
    public class FileMetaEntity : AuditableEntity
    {
        public long CategoryId { get; private set; }
        public virtual CategoryEntity Category { get; private set; } = null!;

        public string FileName { get; private set; } = string.Empty;
        public string Extension { get; private set; } = string.Empty;
        public FileTypesEnum Format { get; private set; }
        public long SizeBytes { get; private set; }

        /// <summary>Modification time of the uploaded file as the browser reported it.</summary>
        public DateTime LastWriteTime { get; private set; }

        /// <summary>
        /// SHA-256 of the file content (64 hex chars) — дедупликация. One stored object per content.
        /// </summary>
        public string FileHash { get; private set; } = string.Empty;

        /// <summary>The object in VStorage that holds the content.</summary>
        public Guid StorageObjectId { get; private set; }

        /// <summary>The VStorage key, e.g. "Library/Math/Geometry.pdf".</summary>
        public string StorageKey { get; private set; } = string.Empty;

        public int? PageCount { get; set; }
        public DateTime LastSeenAt { get; private set; }
        public FileStatus Status { get; private set; } = FileStatus.Ok;

        /// <summary>The file the book opens by default (one per book).</summary>
        public bool IsPrimary { get; private set; }

        /// <summary>
        /// Soft delete: "removed from library", the object stays in VStorage. The record is kept as a tombstone,
        /// so the same content is reported and skipped on later imports.
        /// </summary>
        public bool IsDeleted { get; set; }

        /// <summary>
        /// When an import first reported this tombstone ("same content as deleted book"); later imports only count it.
        /// </summary>
        public DateTime? DeletedReportedAt { get; private set; }

        public ExtractedMetadata? ExtractedMetadata { get; set; }

        /// <summary>
        /// Words key (<see cref="TextNormalizer.WordsKey"/>) of the title stored inside the file (PDF/EPUB/DjVu metadata);
        /// null when the file has no usable embedded title.
        /// </summary>
        public string? ContentTitleKey { get; set; }

        public long? BookId { get; set; }
        public virtual BookEntity? Book { get; set; }
        public virtual ICollection<DuplicateCandidateEntity> DuplicateCandidates { get; set; } = new List<DuplicateCandidateEntity>();

        /// <summary>Cover thumbnail (PDF/EPUB/DjVu); null when none could be made.</summary>
        public virtual FileCoverEntity? Cover { get; private set; }

        /// <summary>When a cover was last attempted (with or without result); null = not tried yet.</summary>
        public DateTime? CoverCheckedAt { get; private set; }

        public bool IsAvailable => Status == FileStatus.Ok;

        /// <summary>The VStorage key of a file in a category.</summary>
        public static string StorageKeyFor(string categoryPath, string fileName) =>
            categoryPath + CategoryEntity.Separator + fileName;

        /// <summary>A file that was uploaded to VStorage as <paramref name="storageObjectId"/>.</summary>
        public static FileMetaEntity Create(
            CategoryEntity category,
            string fileName,
            long sizeBytes,
            DateTime lastWriteTimeUtc,
            string fileHash,
            Guid storageObjectId,
            DateTime utcNow)
        {
            var file = new FileMetaEntity
            {
                Category = category,
                CategoryId = category.Id,
                FileName = fileName,
                Extension = Path.GetExtension(fileName).ToLowerInvariant(),
                FileHash = fileHash,
                StorageObjectId = storageObjectId,
                StorageKey = StorageKeyFor(category.Path, fileName)
            };

            if (FileFormats.TryFromExtension(file.Extension, out var format))
            {
                file.Format = format;
            }

            file.Touch(sizeBytes, lastWriteTimeUtc, utcNow);
            return file;
        }

        /// <summary>
        /// Size and modification time match what is stored, so the content is assumed unchanged.
        /// Database timestamps keep microseconds only, hence the tolerance.
        /// </summary>
        public bool IsUnchanged(long sizeBytes, DateTime lastWriteTimeUtc) =>
            SizeBytes == sizeBytes && Math.Abs((LastWriteTime - lastWriteTimeUtc).TotalMilliseconds) < 1;

        public void MarkSeen(DateTime utcNow)
        {
            LastSeenAt = utcNow;
            Status = FileStatus.Ok;
        }

        /// <summary>Same content, only the modification time changed.</summary>
        public void Touch(long sizeBytes, DateTime lastWriteTimeUtc, DateTime utcNow)
        {
            SizeBytes = sizeBytes;
            LastWriteTime = DateTime.SpecifyKind(lastWriteTimeUtc, DateTimeKind.Utc);
            MarkSeen(utcNow);
        }

        /// <summary>The object is no longer in VStorage (library check).</summary>
        public void MarkMissing() => Status = FileStatus.Missing;

        public void MarkDeletedReported(DateTime utcNow) => DeletedReportedAt = utcNow;

        public void SetCover(int width, int height, byte[] jpeg, DateTime utcNow)
        {
            if (Cover == null)
            {
                Cover = FileCoverEntity.Create(width, height, jpeg);
            }
            else
            {
                Cover.Replace(width, height, jpeg);
            }

            CoverCheckedAt = utcNow;
        }

        /// <summary>A cover was attempted but none could be made (unsupported, damaged, tool missing).</summary>
        public void MarkCoverChecked(DateTime utcNow) => CoverCheckedAt = utcNow;

        public void MakePrimary() => IsPrimary = true;

        public void ClearPrimary() => IsPrimary = false;

        /// <summary>D7: the book is kept without this file; the record stays as meta info only.</summary>
        public void Detach()
        {
            BookId = null;
            Book = null;
            IsPrimary = false;
        }
    }
}
