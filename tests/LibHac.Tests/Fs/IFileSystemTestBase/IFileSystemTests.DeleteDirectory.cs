using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void DeleteDirectory_DoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.DeleteDirectory("/dir".ToU8Span());

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteDirectory_DirectoryExists_EntryIsRemoved()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());

            Result rcDelete = fs.DeleteDirectory("/dir".ToU8Span());
            Result rcEntry = fs.GetEntryType(out _, "/dir".ToU8Span());

            Assert.True(rcDelete.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry);
        }

        [Fact]
        public void DeleteDirectory_PathIsFile_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Result rc = fs.DeleteDirectory("/file".ToU8Span());

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteDirectory_HasOlderSibling_SiblingNotDeleted()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1".ToU8Span());
            fs.CreateDirectory("/dir2".ToU8Span());

            Result rcDelete = fs.DeleteDirectory("/dir2".ToU8Span());
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1".ToU8Span());
            Result rcEntry2 = fs.GetEntryType(out _, "/dir2".ToU8Span());

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
        }

        [Fact]
        public void DeleteDirectory_HasYoungerSibling_SiblingNotDeleted()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir2".ToU8Span());
            fs.CreateDirectory("/dir1".ToU8Span());

            Result rcDelete = fs.DeleteDirectory("/dir2".ToU8Span());
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1".ToU8Span());
            Result rcEntry2 = fs.GetEntryType(out _, "/dir2".ToU8Span());

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
        }

        [Fact]
        public void DeleteDirectory_NotEmpty_ReturnsDirectoryNotEmpty()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());
            fs.CreateFile("/dir/file".ToU8Span(), 0, CreateFileOptions.None);

            Result rc = fs.DeleteDirectory("/dir".ToU8Span());

            Assert.Equal(ResultFs.DirectoryNotEmpty.Value, rc);
        }
    }
}