using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Domain.Tests
{
    public class EntityRuleTests
    {
        private static readonly DateTime Now = new(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);

        [Theory]
        [InlineData(0)]
        [InlineData(11)]
        public void SetRating_OutOfRange_Fails(int rating)
        {
            var book = new BookEntity { Title = "Book" };

            book.SetRating(rating).IsFailure.Should().BeTrue();
            book.Rating.Should().BeNull();
        }

        [Fact]
        public void SetRating_InRange_IsStored()
        {
            var book = new BookEntity { Title = "Book" };

            book.SetRating(7).IsSuccess.Should().BeTrue();
            book.Rating.Should().Be(7);
        }

        [Fact]
        public void MarkOpened_NotStarted_BecomesInProgress()
        {
            var book = new BookEntity { Title = "Book" };

            book.MarkOpened(Now);

            book.LastOpenedAt.Should().Be(Now);
            book.ReadingStatus.Should().Be(ReadingStatus.InProgress);
        }

        [Fact]
        public void MarkOpened_Read_StaysRead()
        {
            var book = new BookEntity { Title = "Book", ReadingStatus = ReadingStatus.Read };

            book.MarkOpened(Now);

            book.ReadingStatus.Should().Be(ReadingStatus.Read);
        }

        [Fact]
        public void CreateFromMetadata_UsesFallbackTitleAndNormalizesIsbn()
        {
            var book = BookEntity.CreateFromMetadata(new ExtractedMetadata { Isbn = "0-306-40615-2", Language = "ru" }, "file name");

            book.Title.Should().Be("file name");
            book.ISBN.Should().Be("9780306406157");
            book.Language.Should().Be(LanguagesEnum.Russian);
        }

        [Fact]
        public void RefreshSearchText_ContainsAuthorsAndTags()
        {
            var book = new BookEntity { Title = "Book" };
            book.AddAuthor(AuthorInfoEntity.Create(PersonName.Parse("Лев Толстой")!));
            book.Tags.Add(TagEntity.Create("Классика"));

            book.RefreshSearchText();

            book.SearchText.Should().Be("Толстой Лев Классика");
        }

        [Fact]
        public void NoteReview_Remember_MovesToNextBox()
        {
            var note = BookNoteEntity.Create(1, NoteType.Note, "text", 10, null, Now).Value;

            note.Review(ReviewAnswer.Remember, Now);

            note.ReviewBox.Should().Be(2);
            note.NextReviewAt.Should().Be(Now.AddDays(3));
            note.ReviewCount.Should().Be(1);
        }

        [Fact]
        public void NoteReview_Forgot_ReturnsToFirstBox()
        {
            var note = BookNoteEntity.Create(1, NoteType.Quote, "text", null, null, Now).Value;
            note.Review(ReviewAnswer.Remember, Now);
            note.Review(ReviewAnswer.Remember, Now);

            note.Review(ReviewAnswer.Forgot, Now);

            note.ReviewBox.Should().Be(1);
            note.NextReviewAt.Should().Be(Now.AddDays(1));
        }

        [Fact]
        public void NoteReview_Skip_PostponesWithoutCounting()
        {
            var note = BookNoteEntity.Create(1, NoteType.Note, "text", null, null, Now).Value;

            note.Review(ReviewAnswer.Skip, Now.AddDays(5));

            note.ReviewBox.Should().Be(1);
            note.ReviewCount.Should().Be(0);
            note.NextReviewAt.Should().Be(Now.AddDays(6));
        }

        [Fact]
        public void NoteCreate_EmptyText_Fails()
        {
            BookNoteEntity.Create(1, NoteType.Note, "  ", null, null, Now).IsFailure.Should().BeTrue();
        }

        [Fact]
        public void FileMeta_IsUnchanged_ToleratesSubMillisecondDifference()
        {
            var books = CategoryEntity.CreateTopLevel("Books").Value;
            var file = FileMetaEntity.Create(books, "a.pdf", 100, Now, new string('a', 64), Guid.NewGuid(), Now);

            file.IsUnchanged(100, Now.AddTicks(5)).Should().BeTrue();
            file.IsUnchanged(101, Now).Should().BeFalse();
            file.Format.Should().Be(FileTypesEnum.PDF);
        }
    }
}
