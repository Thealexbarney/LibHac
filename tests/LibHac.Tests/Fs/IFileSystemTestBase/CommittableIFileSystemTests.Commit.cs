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
            fs.CreateDirectory("/dir1".ToU8Span()).ThrowIfFailure();
            fs.CreateDirectory("/dir2".ToU8Span()).ThrowIfFailure();

            fs.CreateFile("/dir1/file".ToU8Span(), data1.Length, CreateFileOptions.None).ThrowIfFailure();
            fs.CreateFile("/dir2/file".ToU8Span(), data2.Length, CreateFileOptions.None).ThrowIfFailure();

            fs.OpenFile(out IFile file1, "/dir1/file".ToU8Span(), OpenMode.Write).ThrowIfFailure();
            fs.OpenFile(out IFile file2, "/dir2/file".ToU8Span(), OpenMode.Write).ThrowIfFailure();

            file1.Write(0, data1, WriteOption.Flush).ThrowIfFailure();
            file2.Write(0, data2, WriteOption.Flush).ThrowIfFailure();

            file1.Dispose();
            file2.Dispose();

            fs.Commit().ThrowIfFailure();
            fs.Dispose();

            // Reopen after committing
            fs = fsCreator.Create();

            byte[] readData1 = new byte[data1.Length];
            byte[] readData2 = new byte[data2.Length];

            Assert.Success(fs.OpenFile(out file1, "/dir1/file".ToU8Span(), OpenMode.Read));

            using (file1)
            {
                Assert.Success(file1.Read(out long bytesRead, 0, readData1, ReadOption.None));
                Assert.Equal(data1.Length, bytesRead);
            }

            Assert.Equal(data1, readData1);

            Assert.Success(fs.OpenFile(out file2, "/dir2/file".ToU8Span(), OpenMode.Read));

            using (file2)
            {
                Assert.Success(file2.Read(out long bytesRead, 0, readData2, ReadOption.None));
                Assert.Equal(data2.Length, bytesRead);
            }

            Assert.Equal(data2, readData2);
        }

        [Fact]
        public void Rollback_CreateFileThenRollback_FileDoesNotExist()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span()).ThrowIfFailure();
            fs.CreateFile("/dir/file".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();

            // Rollback should succeed
            Assert.Success(fs.Rollback());

            // Make sure the file and directory are gone
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir".ToU8Span()));
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir/file".ToU8Span()));
        }

        [Fact]
        public void Rollback_CreateFileThenCloseFs_FileDoesNotExist()
        {
            IReopenableFileSystemCreator fsCreator = GetFileSystemCreator();
            IFileSystem fs = fsCreator.Create();

            fs.CreateDirectory("/dir".ToU8Span()).ThrowIfFailure();
            fs.CreateFile("/dir/file".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();

            // Close without committing and reopen the file system
            fs.Dispose();
            fs = fsCreator.Create();

            // Make sure the file and directory are gone
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir".ToU8Span()));
            Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "/dir/file".ToU8Span()));
        }

        [Fact]
        public void Rollback_AfterChangingExistingFiles_GoesBackToOriginalData()
        {
            // "Random" test data
            byte[] data1 = { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };
            byte[] data2 = { 6, 1, 6, 8, 0, 3, 9, 7, 5, 1 };

            IReopenableFileSystemCreator fsCreator = GetFileSystemCreator();
            IFileSystem fs = fsCreator.Create();

            fs.CreateDirectory("/dir".ToU8Span()).ThrowIfFailure();
            fs.CreateFile("/dir/file".ToU8Span(), data1.Length, CreateFileOptions.None).ThrowIfFailure();

            fs.OpenFile(out IFile file, "/dir/file".ToU8Span(), OpenMode.Write).ThrowIfFailure();
            file.Write(0, data1, WriteOption.Flush).ThrowIfFailure();
            file.Dispose();

            // Commit and reopen the file system
            fs.Commit().ThrowIfFailure();
            fs.Dispose();

            fs = fsCreator.Create();

            // Make changes to the file
            fs.OpenFile(out file, "/dir/file".ToU8Span(), OpenMode.Write).ThrowIfFailure();
            file.Write(0, data2, WriteOption.Flush).ThrowIfFailure();
            file.Dispose();

            Assert.Success(fs.Rollback());

            // The file should contain the original data after the rollback
            byte[] readData = new byte[data1.Length];

            Assert.Success(fs.OpenFile(out file, "/dir/file".ToU8Span(), OpenMode.Read));

            using (file)
            {
                Assert.Success(file.Read(out long bytesRead, 0, readData, ReadOption.None));
                Assert.Equal(data1.Length, bytesRead);
            }

            Assert.Equal(data1, readData);
        }
    }
}
