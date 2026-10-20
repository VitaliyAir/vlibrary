using VLibrary.Application.Abstractions;
using VLibrary.Application.Features.Imports;
using VLibrary.Domain.Enums;
using VLibrary.Domain.Rules;

namespace VLibrary.Application.Tests.Imports
{
    public class DuplicateMatcherTests
    {
        [Fact]
        public void Isbn_Wins_With100()
        {
            var matches = DuplicateMatcher.Match([new BookCandidate(1, IsbnEquals: true, false, false, false, SizeEquals: true)]);

            matches.Should().ContainSingle().Which.Should().Be(new BookMatch(1, 1.0, DuplicateReason.IsbnMatch));
        }

        [Theory]
        [InlineData(false, true, false, false, DuplicateReason.TitleMatch, 1.0)]
        [InlineData(false, false, true, false, DuplicateReason.TitleWordsMatch, 0.9)]
        [InlineData(false, false, false, true, DuplicateReason.ContentTitleMatch, 0.9)]
        [InlineData(true, false, false, false, DuplicateReason.SizeMatch, 0.8)]
        public void EachRule_HasItsScore(bool size, bool title, bool words, bool content, DuplicateReason reason, double score)
        {
            var matches = DuplicateMatcher.Match([new BookCandidate(7, false, title, words, content, size)]);

            matches.Should().ContainSingle().Which.Should().Be(new BookMatch(7, score, reason));
        }

        [Fact]
        public void NoRule_IsIgnored()
        {
            DuplicateMatcher.Match([new BookCandidate(3, false, false, false, false, false)]).Should().BeEmpty();
        }

        [Theory]
        [InlineData("Кенгуру 3-4 2012", "кенгуру 2012 3-4")]
        [InlineData("Кунгуру 3-4 2012", "Кунгуру 3-4_2012")]
        public void TitleWords_IgnoreOrderAndSeparators(string a, string b)
        {
            TextNormalizer.WordsKey(a).Should().Be(TextNormalizer.WordsKey(b));
        }

        [Fact]
        public void DifferentClassOrYear_DoNotMatch()
        {
            TextNormalizer.WordsKey("kenguru 2012 class 3-4").Should().NotBe(TextNormalizer.WordsKey("kenguru 2012 class 2"));
            TextNormalizer.WordsKey("kenguru 2012 class 3-4").Should().NotBe(TextNormalizer.WordsKey("kenguru 1997 class 3-4"));
        }
    }
}
