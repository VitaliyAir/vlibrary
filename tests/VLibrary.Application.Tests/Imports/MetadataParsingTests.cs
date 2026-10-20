using VLibrary.Application.Features.Imports.Metadata;
using VLibrary.Domain.ValueObjects;

namespace VLibrary.Application.Tests.Imports
{
    public class MetadataParsingTests
    {
        [Fact]
        public void FileName_AuthorTitleYear()
        {
            var metadata = FileNameParser.Parse("Толстой Л.Н. - Война и мир (1869)");

            metadata.Authors.Should().Equal("Толстой Л.Н.");
            metadata.Title.Should().Be("Война и мир");
            metadata.Year.Should().Be(1869);
            metadata.Source.Should().Be(FileNameParser.SourceName);
        }

        [Fact]
        public void FileName_TitleOnly_WithUnderscores()
        {
            var metadata = FileNameParser.Parse("Introduction_to_Algorithms_[2009]");

            metadata.Authors.Should().BeEmpty();
            metadata.Title.Should().Be("Introduction to Algorithms");
            metadata.Year.Should().Be(2009);
        }

        [Fact]
        public void FileName_SeveralAuthors()
        {
            var metadata = FileNameParser.Parse("Kernighan, Ritchie - The C Programming Language");

            metadata.Authors.Should().Equal("Kernighan, Ritchie");
            metadata.Title.Should().Be("The C Programming Language");
        }

        [Theory]
        [InlineData("Tolstoy, Leo", new[] { "Tolstoy, Leo" })]
        [InlineData("A. Smith; B. Jones", new[] { "A. Smith", "B. Jones" })]
        [InlineData("Ильф И. и Петров Е.", new[] { "Ильф И.", "Петров Е." })]
        [InlineData("Brian Kernighan, Dennis Ritchie", new[] { "Brian Kernighan", "Dennis Ritchie" })]
        [InlineData("", new string[0])]
        public void AuthorNames_Split(string raw, string[] expected)
        {
            AuthorNamesSplitter.Split(raw).Should().Equal(expected);
        }

        [Theory]
        [InlineData("Microsoft Word - draft.doc", true)]
        [InlineData("untitled", true)]
        [InlineData("123", true)]
        [InlineData("my-book-file", true)]
        [InlineData("Война и мир", false)]
        public void JunkTitles_AreDetected(string title, bool isJunk)
        {
            MetadataMerger.IsJunkTitle(title, "my-book-file").Should().Be(isJunk);
        }

        [Fact]
        public void Merge_JunkEmbeddedValues_FallBackToFileName()
        {
            var fromFile = new ExtractedMetadata
            {
                Title = "Microsoft Word - book.doc",
                Authors = ["Administrator"],
                Isbn = "0-306-40615-2",
                PageCount = 320,
                Source = "pdf"
            };
            var fromName = FileNameParser.Parse("Толстой Л.Н. - Война и мир (1869)");

            var merged = MetadataMerger.Merge(fromFile, fromName, "Толстой Л.Н. - Война и мир (1869)");

            merged.Title.Should().Be("Война и мир");
            merged.Source.Should().Be(FileNameParser.SourceName);
            merged.Authors.Should().Equal("Толстой Л.Н.");
            merged.Isbn.Should().Be("9780306406157");
            merged.Year.Should().Be(1869);
            merged.PageCount.Should().Be(320);
        }
    }
}
