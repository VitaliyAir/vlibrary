using VLibrary.Application.Features.Imports;

namespace VLibrary.Application.Tests.Imports
{
    public class ImportPathTests
    {
        [Fact]
        public void Parse_SplitsFoldersAndFileName()
        {
            var path = ImportPath.Parse(@"Library\Math/ Geometry /book.pdf").Value;

            path.Folders.Should().Equal("Library", "Math", "Geometry");
            path.FileName.Should().Be("book.pdf");
        }

        [Fact]
        public void CategoryPath_StartsWithTheParent()
        {
            var path = ImportPath.Parse("Library/Math/book.pdf").Value;

            path.CategoryPath(null).Should().Be("Library/Math");
            path.CategoryPath("Books/Science").Should().Be("Books/Science/Library/Math");
        }

        [Fact]
        public void FileWithoutFolder_HasNoCategoryOfItsOwn()
        {
            var path = ImportPath.Parse("book.pdf").Value;

            path.Folders.Should().BeEmpty();
            path.CategoryPath(null).Should().BeEmpty();
            path.CategoryPath("Library").Should().Be("Library");
        }

        [Theory]
        [InlineData("")]
        [InlineData("/")]
        [InlineData("Library/../book.pdf")]
        [InlineData("Library/./book.pdf")]
        public void InvalidPath_IsRejected(string relativePath)
        {
            ImportPath.Parse(relativePath).IsFailure.Should().BeTrue();
        }
    }
}
