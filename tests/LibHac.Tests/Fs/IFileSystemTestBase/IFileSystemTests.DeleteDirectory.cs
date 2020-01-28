using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void DeleteDirectory_DoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.DeleteDirectory("/dir");

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteDirectory_DirectoryExists_EntryIsRemoved()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir");

            Result rcDelete = fs.DeleteDirectory("/dir");
            Result rcEntry = fs.GetEntryType(out _, "/dir");

            Assert.True(rcDelete.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry);
        }

        [Fact]
        public void DeleteDirectory_PathIsFile_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rc = fs.DeleteDirectory("/file");

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteDirectory_HasOlderSibling_SiblingNotDeleted()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir2");

            Result rcDelete = fs.DeleteDirectory("/dir2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
            Result rcEntry2 = fs.GetEntryType(out _, "/dir2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
        }

        [Fact]
        public void DeleteDirectory_HasYoungerSibling_SiblingNotDeleted()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir2");
            fs.CreateDirectory("/dir1");

            Result rcDelete = fs.DeleteDirectory("/dir2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
            Result rcEntry2 = fs.GetEntryType(out _, "/dir2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
        }

        [Fact]
        public void DeleteDirectory_NotEmpty_ReturnsDirectoryNotEmpty()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir");
            fs.CreateFile("/dir/file", 0, CreateFileOptions.None);

            Result rc = fs.DeleteDirectory("/dir");

            Assert.Equal(ResultFs.DirectoryNotEmpty.Value, rc);
        }
    }
}