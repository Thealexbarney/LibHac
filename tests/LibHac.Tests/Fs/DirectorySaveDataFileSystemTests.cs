using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Fs.IFileSystemTestBase;
using Xunit;

namespace LibHac.Tests.Fs
{
    public class DirectorySaveDataFileSystemTests : CommittableIFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            return CreateFileSystemInternal().saveFs;
        }

        protected override IReopenableFileSystemCreator GetFileSystemCreator()
        {
            return new DirectorySaveDataFileSystemCreator();
        }

        private class DirectorySaveDataFileSystemCreator : IReopenableFileSystemCreator
        {
            private IFileSystem BaseFileSystem { get; }

            public DirectorySaveDataFileSystemCreator()
            {
                BaseFileSystem = new InMemoryFileSystem();
            }

            public IFileSystem Create()
            {
                DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, BaseFileSystem, true, true)
                    .ThrowIfFailure();

                return saveFs;
            }
        }

        private (IFileSystem baseFs, IFileSystem saveFs) CreateFileSystemInternal()
        {
            var baseFs = new InMemoryFileSystem();

            DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, baseFs, true, true)
                .ThrowIfFailure();

            return (baseFs, saveFs);
        }

        [Fact]
        public void CreateFile_CreatedInWorkingDirectory()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/1/file".ToU8Span()));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateFile_NotCreatedInCommittedDirectory()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/0/file".ToU8Span()));
        }

        [Fact]
        public void Commit_FileExistsInCommittedDirectory()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Success(saveFs.Commit());

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/0/file".ToU8Span()));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void Rollback_FileDoesNotExistInBaseAfterRollback()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            // Rollback should succeed
            Assert.Success(saveFs.Rollback());

            // Make sure all the files are gone
            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file".ToU8Span()));
            Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/0/file".ToU8Span()));
            Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/1/file".ToU8Span()));
        }

        [Fact]
        public void Rollback_DeletedFileIsRestoredInBaseAfterRollback()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);
            saveFs.Commit();
            saveFs.DeleteFile("/file".ToU8Span());

            // Rollback should succeed
            Assert.Success(saveFs.Rollback());

            // Make sure all the files are restored
            Assert.Success(saveFs.GetEntryType(out _, "/file".ToU8Span()));
            Assert.Success(baseFs.GetEntryType(out _, "/0/file".ToU8Span()));
            Assert.Success(baseFs.GetEntryType(out _, "/1/file".ToU8Span()));
        }

        [Fact]
        public void Initialize_NormalState_UsesCommittedData()
        {
            var baseFs = new InMemoryFileSystem();

            baseFs.CreateDirectory("/0".ToU8Span()).ThrowIfFailure();
            baseFs.CreateDirectory("/1".ToU8Span()).ThrowIfFailure();

            // Set the existing files before initializing the save FS
            baseFs.CreateFile("/0/file1".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();
            baseFs.CreateFile("/1/file2".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();

            DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, baseFs, true, true)
                .ThrowIfFailure();

            Assert.Success(saveFs.GetEntryType(out _, "/file1".ToU8Span()));
            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file2".ToU8Span()));
        }

        [Fact]
        public void Initialize_InterruptedAfterCommitPart1_UsesWorkingData()
        {
            var baseFs = new InMemoryFileSystem();

            baseFs.CreateDirectory("/_".ToU8Span()).ThrowIfFailure();
            baseFs.CreateDirectory("/1".ToU8Span()).ThrowIfFailure();

            // Set the existing files before initializing the save FS
            baseFs.CreateFile("/_/file1".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();
            baseFs.CreateFile("/1/file2".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();

            DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, baseFs, true, true)
                .ThrowIfFailure();

            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file1".ToU8Span()));
            Assert.Success(saveFs.GetEntryType(out _, "/file2".ToU8Span()));
        }

        [Fact]
        public void Initialize_InterruptedDuringCommitPart2_UsesWorkingData()
        {
            var baseFs = new InMemoryFileSystem();

            baseFs.CreateDirectory("/1".ToU8Span()).ThrowIfFailure();

            // Set the existing files before initializing the save FS
            baseFs.CreateFile("/1/file2".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();

            DirectorySaveDataFileSystem.CreateNew(out DirectorySaveDataFileSystem saveFs, baseFs, true, true)
                .ThrowIfFailure();

            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file1".ToU8Span()));
            Assert.Success(saveFs.GetEntryType(out _, "/file2".ToU8Span()));
        }
    }
}
