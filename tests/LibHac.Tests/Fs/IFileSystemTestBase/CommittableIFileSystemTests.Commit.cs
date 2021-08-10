using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class CommittableIFileSystemTests
    {
        [Fact]
        public void Commit_AfterSuccessfulCommit_CanReadCommittedData()
        {
            // "Random" test data
            byte[] data1 = { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };
            byte[] data2 = { 6, 1, 6, 8, 0, 3, 9, 7, 5, 1 };

            IReopenableFileSystemCreator fsCreator = GetFileSystemCreator();
            IFileSystem fs = fsCreator.Create();

            // Make sure to test both directories and files
            fs.CreateDirectory("/dir1").ThrowIfFailure();
            fs.CreateDirectory("/dir2").ThrowIfFailure();

            fs.CreateFile("/dir1/file", data1.Length, CreateFileOptions.None).ThrowIfFailure();
            fs.CreateFile("/dir2/file", data2.Length, CreateFileOptions.None).ThrowIfFailure();

            using var file1 = new UniqueRef<IFile>();
            using var file2 = new UniqueRef<IFile>();

            fs.OpenFile(ref file1.Ref(), "/dir1/file", OpenMode.Write).ThrowIfFailure();
            fs.OpenFile(ref file2.Ref(), "/dir2/file", OpenMode.Write).ThrowIfFailure();

            file1.Get.Write(0, data1, WriteOption.Flush).ThrowIfFailure();
            file2.Get.Write(0, data2, WriteOption.Flush).ThrowIfFailure();

            file1.Reset();
            file2.Reset();

            fs.Commit().ThrowIfFailure();
            fs.Dispose();

            // Reopen after committing
            fs = fsCreator.Create();

            byte[] readData1 = new byte[data1.Length];
            byte[] readData2 = new byte[data2.Length];

            Assert.Success(fs.OpenFile(ref file1.Ref(), "/dir1/file", OpenMode.Read));

            Assert.Success(file1.Get.Read(out long bytesReadFile1, 0, readData1, ReadOption.None));
            file1.Reset();
            Assert.Equal(data1.Length, bytesReadFile1);

            Assert.Equal(data1, readData1);

            Assert.Success(fs.OpenFile(ref file2.Ref(), "/dir2/file", OpenMode.Read));

            Assert.Success(file2.Get.Read(out long bytesReadFile2, 0, readData2, ReadOption.None));
            Assert.Equal(data2.Length, bytesReadFile2);

            Assert.Equal(data2, readData2);
        }

        [Fact]
        public void Rollback_CreateFileThenRollback_FileDoesNotExist()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir").ThrowIfFailure();
            fs.CreateFile("/dir/file", 0, CreateFileOptions.None).ThrowIfFailure();

            // Rollback should succeed
            Assert.Success(fs.Rollback());

            // Make sure the file and directory are gone
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir"));
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir/file"));
        }

        [Fact]
        public void Rollback_CreateFileThenCloseFs_FileDoesNotExist()
        {
            IReopenableFileSystemCreator fsCreator = GetFileSystemCreator();
            IFileSystem fs = fsCreator.Create();

            fs.CreateDirectory("/dir").ThrowIfFailure();
            fs.CreateFile("/dir/file", 0, CreateFileOptions.None).ThrowIfFailure();

            // Close without committing and reopen the file system
            fs.Dispose();
            fs = fsCreator.Create();

            // Make sure the file and directory are gone
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir"));
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir/file"));
        }

        [Fact]
        public void Rollback_AfterChangingExistingFiles_GoesBackToOriginalData()
        {
            // "Random" test data
            byte[] data1 = { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };
            byte[] data2 = { 6, 1, 6, 8, 0, 3, 9, 7, 5, 1 };

            IReopenableFileSystemCreator fsCreator = GetFileSystemCreator();
            IFileSystem fs = fsCreator.Create();

            fs.CreateDirectory("/dir").ThrowIfFailure();
            fs.CreateFile("/dir/file", data1.Length, CreateFileOptions.None).ThrowIfFailure();

            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/dir/file", OpenMode.Write).ThrowIfFailure();
            file.Get.Write(0, data1, WriteOption.Flush).ThrowIfFailure();
            file.Reset();

            // Commit and reopen the file system
            fs.Commit().ThrowIfFailure();
            fs.Dispose();

            fs = fsCreator.Create();

            // Make changes to the file
            fs.OpenFile(ref file.Ref(), "/dir/file", OpenMode.Write).ThrowIfFailure();
            file.Get.Write(0, data2, WriteOption.Flush).ThrowIfFailure();
            file.Reset();

            Assert.Success(fs.Rollback());

            // The file should contain the original data after the rollback
            byte[] readData = new byte[data1.Length];

            Assert.Success(fs.OpenFile(ref file.Ref(), "/dir/file", OpenMode.Read));

            Assert.Success(file.Get.Read(out long bytesRead, 0, readData, ReadOption.None));
            Assert.Equal(data1.Length, bytesRead);

            Assert.Equal(data1, readData);
        }
    }
}
