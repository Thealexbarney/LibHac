using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void CreateDirectory_EntryIsAdded()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/dir".ToU8Span());

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type);
        }

        [Fact]
        public void CreateDirectory_DirectoryExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());

            Result rc = fs.CreateDirectory("/dir".ToU8Span());

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rc);
        }

        [Fact]
        public void CreateDirectory_FileExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Result rc = fs.CreateDirectory("/file".ToU8Span());

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rc);
        }

        [Fact]
        public void CreateDirectory_ParentDoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.CreateFile("/dir1/dir2".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void CreateDirectory_WithTrailingSeparator_EntryIsAdded()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir/".ToU8Span());
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/dir/".ToU8Span());

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type);
        }

        [Fact]
        public void CreateDirectory_MultipleSiblings()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1".ToU8Span());
            fs.CreateDirectory("/dir2".ToU8Span());

            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1".ToU8Span());
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2".ToU8Span());

            Assert.True(rc1.IsSuccess());
            Assert.True(rc2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type1);
            Assert.Equal(DirectoryEntryType.Directory, type2);
        }

        [Fact]
        public void CreateDirectory_InChildDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1".ToU8Span());
            fs.CreateDirectory("/dir2".ToU8Span());

            fs.CreateDirectory("/dir1/dir1a".ToU8Span());
            fs.CreateDirectory("/dir2/dir2a".ToU8Span());

            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1/dir1a".ToU8Span());
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2/dir2a".ToU8Span());

            Assert.True(rc1.IsSuccess());
            Assert.True(rc2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type1);
            Assert.Equal(DirectoryEntryType.Directory, type2);
        }
    }
}