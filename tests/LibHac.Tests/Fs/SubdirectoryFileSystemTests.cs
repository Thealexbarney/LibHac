using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Tests.Fs.IFileSystemTestBase;
using Xunit;

namespace LibHac.Tests.Fs
{
    public class SubdirectoryFileSystemTests : IFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            return CreateFileSystemInternal().subDirFs;
        }

        private (IFileSystem baseFs, IFileSystem subDirFs) CreateFileSystemInternal()
        {
            var baseFs = new InMemoryFileSystem();
            baseFs.CreateDirectory("/sub");
            baseFs.CreateDirectory("/sub/path");

            SubdirectoryFileSystem.CreateNew(out SubdirectoryFileSystem subFs, baseFs, "/sub/path".ToU8String()).ThrowIfFailure();
            return (baseFs, subFs);
        }

        [Fact]
        public void CreateFile_CreatedInBaseFileSystem()
        {
            (IFileSystem baseFs, IFileSystem subDirFs) = CreateFileSystemInternal();

            subDirFs.CreateFile("/file", 0, CreateFileOptions.None);
            Result rc = baseFs.GetEntryType(out DirectoryEntryType type, "/sub/path/file");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateDirectory_CreatedInBaseFileSystem()
        {
            (IFileSystem baseFs, IFileSystem subDirFs) = CreateFileSystemInternal();

            subDirFs.CreateDirectory("/dir");
            Result rc = baseFs.GetEntryType(out DirectoryEntryType type, "/sub/path/dir");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type);
        }
    }

    public class SubdirectoryFileSystemTestsRoot : IFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            var baseFs = new InMemoryFileSystem();

            SubdirectoryFileSystem.CreateNew(out SubdirectoryFileSystem subFs, baseFs, "/".ToU8String()).ThrowIfFailure();
            return subFs;
        }
    }
}
