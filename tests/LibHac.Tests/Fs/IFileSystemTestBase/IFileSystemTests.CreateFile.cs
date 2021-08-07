using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void CreateFile_EntryIsAdded()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/file");

            Assert.Success(rc);
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateFile_DirectoryExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir");

            Result rc = fs.CreateFile("/dir", 0, CreateFileOptions.None);

            Assert.Result(ResultFs.PathAlreadyExists, rc);
        }

        [Fact]
        public void CreateFile_FileExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rc = fs.CreateFile("/file", 0, CreateFileOptions.None);

            Assert.Result(ResultFs.PathAlreadyExists, rc);
        }

        [Fact]
        public void CreateFile_ParentDoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.CreateFile("/dir/file", 0, CreateFileOptions.None);

            Assert.Result(ResultFs.PathNotFound, rc);
        }

        [Fact]
        public void CreateFile_WithTrailingSeparator_EntryIsAdded()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file/", 0, CreateFileOptions.None);

            Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "/file/"));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateFile_WithSize_SizeIsSet()
        {
            const long expectedSize = 12345;

            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", expectedSize, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file", OpenMode.Read);

            Assert.Success(file.GetSize(out long fileSize));
            Assert.Equal(expectedSize, fileSize);
        }

        [Fact]
        public void CreateFile_MultipleSiblings()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/file2", 0, CreateFileOptions.None);

            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/file1");
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/file2");

            Assert.Success(rc1);
            Assert.Success(rc2);
            Assert.Equal(DirectoryEntryType.File, type1);
            Assert.Equal(DirectoryEntryType.File, type2);
        }

        [Fact]
        public void CreateFile_InChildDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir2");

            fs.CreateFile("/dir1/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/dir2/file2", 0, CreateFileOptions.None);

            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1/file1");
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2/file2");

            Assert.Success(rc1);
            Assert.Success(rc2);
            Assert.Equal(DirectoryEntryType.File, type1);
            Assert.Equal(DirectoryEntryType.File, type2);
        }
    }
}
