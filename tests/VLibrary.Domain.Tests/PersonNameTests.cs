using VLibrary.Domain.ValueObjects;

namespace VLibrary.Domain.Tests
{
    public class PersonNameTests
    {
        [Theory]
        [InlineData("Leo Tolstoy", "Leo", null, "Tolstoy", null)]
        [InlineData("Tolstoy, Leo", "Leo", null, "Tolstoy", null)]
        [InlineData("Толстой Лев Николаевич", "Лев", "Николаевич", "Толстой", null)]
        [InlineData("Толстой Л.Н.", null, null, "Толстой", "Л.Н.")]
        [InlineData("Л.Н. Толстой", null, null, "Толстой", "Л.Н.")]
        [InlineData("Tolstoy", null, null, "Tolstoy", null)]
        [InlineData("John Ronald Tolkien", "John", "Ronald", "Tolkien", null)]
        public void Parse_KnownFormats(string raw, string? first, string? middle, string? last, string? initials)
        {
            var name = PersonName.Parse(raw);

            name.Should().Be(new PersonName(first, middle, last, initials));
        }

        [Fact]
        public void MatchKey_InitialsAndFullName_AreEqual()
        {
            PersonName.Parse("Толстой Л.Н.")!.MatchKey
                .Should().Be(PersonName.Parse("Толстой Лев Николаевич")!.MatchKey)
                .And.Be("толстой л");
        }

        [Fact]
        public void DisplayName_UsesLastFirstMiddle()
        {
            PersonName.Parse("Лев Толстой")!.DisplayName.Should().Be("Толстой Лев");
            PersonName.Parse("Толстой Л.Н.")!.DisplayName.Should().Be("Толстой Л.Н.");
        }
    }
}
