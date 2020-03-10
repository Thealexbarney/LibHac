using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void RenameFile_SameParentDirectory_EntryIsRenamed()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1".ToU8Span(), 0, CreateFileOptions.None);

            Assert.True(fs.RenameFile("/file1".ToU8Span(), "/file2".ToU8Span()).IsSuccess());

            Assert.True(fs.GetEntryType(out DirectoryEntryType type, "/file2".ToU8Span()).IsSuccess());
            Result rc = fs.GetEntryType(out _, "/file1".ToU8Span());

            Assert.Equal(DirectoryEntryType.File, type);
            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }
        [Fact]
        public void RenameFile_DifferentParentDirectory_EntryIsRenamed()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1".ToU8Span(), 0, CreateFileOptions.None);
            fs.CreateDirectory("/dir".ToU8Span());

            Assert.True(fs.RenameFile("/file1".ToU8Span(), "/dir/file2".ToU8Span()).IsSuccess());

            Assert.True(fs.GetEntryType(out DirectoryEntryType type, "/dir/file2".ToU8Span()).IsSuccess());
            Result rc = fs.GetEntryType(out _, "/file1".ToU8Span());

            Assert.Equal(DirectoryEntryType.File, type);
            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void RenameFile_DestExistsAsFile_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1".ToU8Span(), 0, CreateFileOptions.None);
            fs.CreateFile("/file2".ToU8Span(), 0, CreateFileOptions.None);

            Result rc = fs.RenameFile("/file1".ToU8Span(), "/file2".ToU8Span());

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rc);
        }

        [Fact]
        public void RenameFile_DestExistsAsDirectory_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);
            fs.CreateDirectory("/dir".ToU8Span());

            Result rc = fs.RenameFile("/file".ToU8Span(), "/dir".ToU8Span());

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rc);
        }

        [Fact]
        public void RenameFile_DestExistsAsFile_FileSizesDoNotChange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1".ToU8Span(), 54321, CreateFileOptions.None);
            fs.CreateFile("/file2".ToU8Span(), 12345, CreateFileOptions.None);

            fs.RenameFile("/file1".ToU8Span(), "/file2".ToU8Span());

            Assert.True(fs.OpenFile(out IFile file1, "/file1".ToU8Span(), OpenMode.Read).IsSuccess());
            Assert.True(fs.OpenFile(out IFile file2, "/file2".ToU8Span(), OpenMode.Read).IsSuccess());

            using (file1)
            using (file2)
            {
                Assert.True(file1.GetSize(out long file1Size).IsSuccess());
                Assert.True(file2.GetSize(out long file2Size).IsSuccess());

                Assert.Equal(54321, file1Size);
                Assert.Equal(12345, file2Size);
            }
        }

        [Fact]
        public void RenameFile_DataIsUnmodified()
        {
            var data = new byte[] { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };

            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), data.Length, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            file.Write(0, data, WriteOption.None);
            file.Dispose();

            fs.RenameFile("/file".ToU8Span(), "/renamed".ToU8Span());

            var readData = new byte[data.Length];

            fs.OpenFile(out file, "/renamed".ToU8Span(), OpenMode.Read);
            Result rc = file.Read(out long bytesRead, 0, readData, ReadOption.None);
            file.Dispose();

            Assert.True(rc.IsSuccess());
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data, readData);
        }
    }
}