using LibHac.Common;
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

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/file".ToU8Span());

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateFile_DirectoryExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());

            Result rc = fs.CreateFile("/dir".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rc);
        }

        [Fact]
        public void CreateFile_FileExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Result rc = fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rc);
        }

        [Fact]
        public void CreateFile_ParentDoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.CreateFile("/dir/file".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void CreateFile_WithTrailingSeparator_EntryIsAdded()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file/".ToU8Span(), 0, CreateFileOptions.None);
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/file/".ToU8Span());

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateFile_WithSize_SizeIsSet()
        {
            const long expectedSize = 12345;

            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), expectedSize, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            Result rc = file.GetSize(out long fileSize);

            Assert.True(rc.IsSuccess());
            Assert.Equal(expectedSize, fileSize);
        }

        [Fact]
        public void CreateFile_MultipleSiblings()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1".ToU8Span(), 0, CreateFileOptions.None);
            fs.CreateFile("/file2".ToU8Span(), 0, CreateFileOptions.None);

            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/file1".ToU8Span());
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/file2".ToU8Span());

            Assert.True(rc1.IsSuccess());
            Assert.True(rc2.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, type1);
            Assert.Equal(DirectoryEntryType.File, type2);
        }

        [Fact]
        public void CreateFile_InChildDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1".ToU8Span());
            fs.CreateDirectory("/dir2".ToU8Span());

            fs.CreateFile("/dir1/file1".ToU8Span(), 0, CreateFileOptions.None);
            fs.CreateFile("/dir2/file2".ToU8Span(), 0, CreateFileOptions.None);

            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1/file1".ToU8Span());
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2/file2".ToU8Span());

            Assert.True(rc1.IsSuccess());
            Assert.True(rc2.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, type1);
            Assert.Equal(DirectoryEntryType.File, type2);
        }
    }
}
