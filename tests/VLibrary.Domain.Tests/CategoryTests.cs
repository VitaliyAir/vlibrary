using VLibrary.Domain.Entities;
using VLibrary.Domain.Enums;

namespace VLibrary.Domain.Tests
{
    public class CategoryTests
    {
        private static readonly DateTime Now = new(2026, 10, 17, 12, 0, 0, DateTimeKind.Utc);
        private static readonly string Hash = new('a', 64);

        [Fact]
        public void Child_BuildsPathFromParent()
        {
            var library = CategoryEntity.CreateTopLevel(" Library ").Value;
            var math = library.CreateChild("Math").Value;

            library.Name.Should().Be("Library");
            math.Path.Should().Be("Library/Math");
            math.Parent.Should().BeSameAs(library);
        }

        [Fact]
        public void HasName_IgnoresCase()
        {
            var category = CategoryEntity.CreateTopLevel("Кенгуру").Value;

            category.HasName("кенгуру").Should().BeTrue();
            category.HasName("Kenguru").Should().BeFalse();
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(".")]
        [InlineData("..")]
        [InlineData("a/b")]
        [InlineData(@"a\b")]
        public void InvalidName_IsRejected(string name)
        {
            CategoryEntity.CreateTopLevel(name).IsFailure.Should().BeTrue();
        }

        [Fact]
        public void SplitPath_AcceptsBothSeparators()
        {
            CategoryEntity.SplitPath(@"Library\Math/Geometry/").Should().Equal("Library", "Math", "Geometry");
        }

        [Fact]
        public void FileMeta_StorageKey_IsCategoryPathAndFileName()
        {
            var math = CategoryEntity.CreateTopLevel("Library").Value.CreateChild("Math").Value;
            var objectId = Guid.NewGuid();

            var file = FileMetaEntity.Create(math, "Geometry.PDF", 10, Now, Hash, objectId, Now);

            file.StorageKey.Should().Be("Library/Math/Geometry.PDF");
            file.Extension.Should().Be(".pdf");
            file.Format.Should().Be(FileTypesEnum.PDF);
            file.StorageObjectId.Should().Be(objectId);
            file.Category.Should().BeSameAs(math);
        }

        [Fact]
        public void FilesState_ReflectsAvailability()
        {
            BookEntity.GetFilesState([]).Should().Be(BookFilesState.NoFiles);
            BookEntity.GetFilesState([FileStatus.Ok, FileStatus.Ok]).Should().Be(BookFilesState.Ok);
            BookEntity.GetFilesState([FileStatus.Ok, FileStatus.Missing]).Should().Be(BookFilesState.Partial);
            BookEntity.GetFilesState([FileStatus.Missing]).Should().Be(BookFilesState.Missing);
        }

        [Fact]
        public void EnsurePrimary_MissingPrimary_MovesToAvailableFile()
        {
            var book = new BookEntity { Title = "Book" };
            var original = NewFile(book, "a.pdf");
            var other = NewFile(book, "a.djvu");
            original.MakePrimary();
            original.MarkMissing();

            book.EnsurePrimary();

            original.IsPrimary.Should().BeFalse();
            other.IsPrimary.Should().BeTrue();
        }

        [Fact]
        public void Detach_KeepsFileAsMetaOnly()
        {
            var book = new BookEntity { Title = "Book" };
            var file = NewFile(book, "a.pdf");
            file.MakePrimary();

            file.Detach();

            file.BookId.Should().BeNull();
            file.IsPrimary.Should().BeFalse();
        }

        private static FileMetaEntity NewFile(BookEntity book, string fileName)
        {
            var file = FileMetaEntity.Create(CategoryEntity.CreateTopLevel("Books").Value, fileName, 10, Now, Hash, Guid.NewGuid(), Now);
            file.Book = book;
            book.FilesMetas.Add(file);
            return file;
        }
    }
}
