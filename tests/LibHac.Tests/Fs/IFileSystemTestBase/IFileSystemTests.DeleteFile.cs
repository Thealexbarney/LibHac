using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void DeleteFile_DoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.DeleteFile("/file");
            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteFile_FileExists_FileEntryIsRemoved()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteFile("/file");
            Result rcEntry = fs.GetEntryType(out _, "/file");

            Assert.True(rcDelete.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry);
        }

        [Fact]
        public void DeleteFile_PathIsDirectory_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir");

            Result rc = fs.DeleteFile("/dir");

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteFile_HasOlderSibling_SiblingNotDeleted()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/file2", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteFile("/file2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/file1");
            Result rcEntry2 = fs.GetEntryType(out _, "/file2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.File, dir1Type);
        }

        [Fact]
        public void DeleteFile_HasYoungerSibling_SiblingNotDeleted()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file2", 0, CreateFileOptions.None);
            fs.CreateFile("/file1", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteFile("/file2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/file1");
            Result rcEntry2 = fs.GetEntryType(out _, "/file2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.File, dir1Type);
        }
    }
}