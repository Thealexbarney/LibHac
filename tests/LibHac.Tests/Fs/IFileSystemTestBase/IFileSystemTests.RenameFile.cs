using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void RenameFile_SameParentDirectory_EntryIsRenamed()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1", 0, CreateFileOptions.None);

            Assert.Success(fs.RenameFile("/file1", "/file2"));

            Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "/file2"));
            Result rc = fs.GetEntryType(out _, "/file1");

            Assert.Equal(DirectoryEntryType.File, type);
            Assert.Result(ResultFs.PathNotFound, rc);
        }
        [Fact]
        public void RenameFile_DifferentParentDirectory_EntryIsRenamed()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1", 0, CreateFileOptions.None);
            fs.CreateDirectory("/dir");

            Assert.Success(fs.RenameFile("/file1", "/dir/file2"));

            Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "/dir/file2"));
            Result rc = fs.GetEntryType(out _, "/file1");

            Assert.Equal(DirectoryEntryType.File, type);
            Assert.Result(ResultFs.PathNotFound, rc);
        }

        [Fact]
        public void RenameFile_DestExistsAsFile_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/file2", 0, CreateFileOptions.None);

            Result rc = fs.RenameFile("/file1", "/file2");

            Assert.Result(ResultFs.PathAlreadyExists, rc);
        }

        [Fact]
        public void RenameFile_DestExistsAsDirectory_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);
            fs.CreateDirectory("/dir");

            Result rc = fs.RenameFile("/file", "/dir");

            Assert.Result(ResultFs.PathAlreadyExists, rc);
        }

        [Fact]
        public void RenameFile_DestExistsAsFile_FileSizesDoNotChange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file1", 54321, CreateFileOptions.None);
            fs.CreateFile("/file2", 12345, CreateFileOptions.None);

            fs.RenameFile("/file1", "/file2");

            using var file1 = new UniqueRef<IFile>();
            using var file2 = new UniqueRef<IFile>();

            Assert.Success(fs.OpenFile(ref file1.Ref(), "/file1", OpenMode.Read));
            Assert.Success(fs.OpenFile(ref file2.Ref(), "/file2", OpenMode.Read));

            Assert.Success(file1.Get.GetSize(out long file1Size));
            Assert.Success(file2.Get.GetSize(out long file2Size));

            Assert.Equal(54321, file1Size);
            Assert.Equal(12345, file2Size);
        }

        [Fact]
        public void RenameFile_DataIsUnmodified()
        {
            byte[] data = { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };

            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", data.Length, CreateFileOptions.None);

            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Write);
            file.Get.Write(0, data, WriteOption.None);
            file.Reset();

            fs.RenameFile("/file", "/renamed");

            byte[] readData = new byte[data.Length];

            fs.OpenFile(ref file.Ref(), "/renamed", OpenMode.Read);
            Result rc = file.Get.Read(out long bytesRead, 0, readData, ReadOption.None);

            Assert.Success(rc);
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data, readData);
        }
    }
}