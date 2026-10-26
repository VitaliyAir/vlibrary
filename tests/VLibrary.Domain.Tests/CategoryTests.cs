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
        public void Relocate_RenamesAndMoves_ChildrenFollowWithMoveUnder()
        {
            var library = CategoryEntity.CreateTopLevel("Library").Value;
            var science = library.CreateChild("Science").Value;
            var physcs = library.CreateChild("Physcs").Value;
            var classic = physcs.CreateChild("Classic").Value;

            physcs.Relocate(science, " Physics ").IsSuccess.Should().BeTrue();
            classic.MoveUnder(physcs).IsSuccess.Should().BeTrue();

            physcs.Path.Should().Be("Library/Science/Physics");
            physcs.HasName("physics").Should().BeTrue();
            classic.Path.Should().Be("Library/Science/Physics/Classic");
        }

        [Fact]
        public void Relocate_RejectsAnInvalidName() =>
            CategoryEntity.CreateTopLevel("A").Value.Relocate(null, "a/b").IsFailure.Should().BeTrue();

        [Fact]
        public void LeaveRedirect_KeepsTheOldNameAndPlace_AndRestoreMakesItLive()
        {
            var library = CategoryEntity.CreateTopLevel("Library").Value;
            var physcs = library.CreateChild("Physcs").Value;

            var redirect = physcs.LeaveRedirect(CategoryRedirect.Renamed);
            physcs.Relocate(library, "Physics");

            redirect.Path.Should().Be("Library/Physcs");
            redirect.IsRedirect.Should().BeTrue();
            redirect.RedirectTo.Should().BeSameAs(physcs);
            physcs.Path.Should().Be("Library/Physics");

            redirect.Restore();
            redirect.IsRedirect.Should().BeFalse();
            redirect.RedirectTo.Should().BeNull();
        }

        [Fact]
        public void PlaceIn_TakesTheCategoryPathIntoTheKey()
        {
            var category = CategoryEntity.CreateTopLevel("Library").Value;
            var file = FileMetaEntity.Create(category, "a.pdf", 1, Now, Hash, Guid.NewGuid(), Now);

            category.Relocate(null, "Books");
            file.PlaceIn(category);

            file.StorageKey.Should().Be("Books/a.pdf");
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

        [Fact]
        public void Rename_KeepsTheExtension_AndRemembersTheOriginalName_RenamingBackForgetsIt()
        {
            var file = FileMetaEntity.Create(CategoryEntity.CreateTopLevel("Sky").Value, "TT_22AA.PDF", 1, Now, Hash, Guid.NewGuid(), Now);

            file.Rename(" Astronomy ").IsSuccess.Should().BeTrue();

            file.FileName.Should().Be("Astronomy.PDF");
            file.StorageKey.Should().Be("Sky/Astronomy.PDF");
            file.OriginalFileName.Should().Be("TT_22AA.PDF");

            file.Rename("Sky");
            file.OriginalFileName.Should().Be("TT_22AA.PDF");

            file.Rename("TT_22AA");
            file.OriginalFileName.Should().BeNull();
        }

        [Theory]
        [InlineData("")]
        [InlineData("a/b")]
        [InlineData("a:b")]
        [InlineData("what?")]
        [InlineData("dot.")]
        public void Rename_RejectsAnInvalidName(string name) =>
            FileMetaEntity.Create(CategoryEntity.CreateTopLevel("Sky").Value, "a.pdf", 1, Now, Hash, Guid.NewGuid(), Now)
                .Rename(name).IsFailure.Should().BeTrue();

        private static FileMetaEntity NewFile(BookEntity book, string fileName)
        {
            var file = FileMetaEntity.Create(CategoryEntity.CreateTopLevel("Books").Value, fileName, 10, Now, Hash, Guid.NewGuid(), Now);
            file.Book = book;
            book.FilesMetas.Add(file);
            return file;
        }
    }
}
