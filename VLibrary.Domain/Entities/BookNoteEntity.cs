using VLibrary.Domain.Common;
using VLibrary.Domain.Enums;

namespace VLibrary.Domain.Entities
{
    public class BookNoteEntity : AuditableEntity
    {
        public const int MinReviewBox = 1;
        public const int MaxReviewBox = 5;

        /// <summary>
        /// Leitner intervals in days for boxes 1..5.
        /// </summary>
        public static readonly IReadOnlyList<int> ReviewIntervalsDays = [1, 3, 7, 21, 60];

        public long BookId { get; set; }
        public virtual BookEntity Book { get; set; } = null!;

        /// <summary>
        /// The file the page number refers to (editions have different page numbers).
        /// </summary>
        public long? FileMetaId { get; set; }
        public virtual FileMetaEntity? FileMeta { get; set; }

        public NoteType Type { get; set; } = NoteType.Note;
        public string Text { get; set; } = string.Empty;
        public int? PageNumber { get; set; }

        public int ReviewBox { get; private set; } = MinReviewBox;
        public DateTime NextReviewAt { get; private set; }
        public DateTime? LastReviewedAt { get; private set; }
        public int ReviewCount { get; private set; }

        public static Result<BookNoteEntity> Create(long bookId, NoteType type, string text, int? pageNumber, long? fileMetaId, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return Error.Validation("Note.Text", "Note text is required.");
            }

            if (pageNumber is < 1)
            {
                return Error.Validation("Note.PageNumber", "Page number must be 1 or greater.");
            }

            return new BookNoteEntity
            {
                BookId = bookId,
                Type = type,
                Text = text.Trim(),
                PageNumber = pageNumber,
                FileMetaId = fileMetaId,
                NextReviewAt = utcNow.AddDays(ReviewIntervalsDays[0])
            };
        }

        /// <summary>
        /// UC-8: Remember moves the note to the next box, Forgot back to box 1, Skip postpones it by a day.
        /// </summary>
        public void Review(ReviewAnswer answer, DateTime utcNow)
        {
            switch (answer)
            {
                case ReviewAnswer.Remember:
                    ReviewBox = Math.Min(ReviewBox + 1, MaxReviewBox);
                    break;
                case ReviewAnswer.Forgot:
                    ReviewBox = MinReviewBox;
                    break;
                case ReviewAnswer.Skip:
                    NextReviewAt = utcNow.AddDays(1);
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(answer), answer, null);
            }

            ReviewCount++;
            LastReviewedAt = utcNow;
            NextReviewAt = utcNow.AddDays(ReviewIntervalsDays[ReviewBox - 1]);
        }
    }
}
