using VLibrary.Domain.Rules;

namespace VLibrary.Domain.Tests
{
    public class IsbnTests
    {
        [Theory]
        [InlineData("978-5-17-090523-2", "9785170905232")]
        [InlineData("ISBN 978-5-17-090523-2", "9785170905232")]
        [InlineData("ISBN-13: 9785170905232", "9785170905232")]
        [InlineData("0-306-40615-2", "9780306406157")]
        [InlineData("080442957X", "9780804429573")]
        [InlineData("urn:isbn:9780306406157", "9780306406157")]
        public void Normalize_ValidIsbn_ReturnsIsbn13(string raw, string expected)
        {
            Isbn.Normalize(raw).Should().Be(expected);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("978-5-17-090523-7")]
        [InlineData("0-306-40615-3")]
        [InlineData("12345")]
        public void Normalize_InvalidIsbn_ReturnsNull(string? raw)
        {
            Isbn.Normalize(raw).Should().BeNull();
        }

        [Fact]
        public void FindInText_LabeledIsbn_IsFound()
        {
            const string imprint = "УДК 51\nББК 22.1\nISBN 978-5-17-090523-2\n© Издательство, 2015";

            Isbn.FindInText(imprint).Should().Be("9785170905232");
        }

        [Fact]
        public void FindInText_UnlabeledNumber_IsIgnored()
        {
            Isbn.FindInText("Phone: 9785170905232").Should().BeNull();
        }
    }
}
