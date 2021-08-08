using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
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

            using var rootPath = new Path();
            PathFunctions.SetUpFixedPath(ref rootPath.Ref(), "/sub/path".ToU8String());

            var subFs = new SubdirectoryFileSystem(baseFs);
            subFs.Initialize(in rootPath).ThrowIfFailure();

            return (baseFs, subFs);
        }

        [Fact]
        public void CreateFile_CreatedInBaseFileSystem()
        {
            (IFileSystem baseFs, IFileSystem subDirFs) = CreateFileSystemInternal();

            subDirFs.CreateFile("/file", 0, CreateFileOptions.None);

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/sub/path/file"));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateDirectory_CreatedInBaseFileSystem()
        {
            (IFileSystem baseFs, IFileSystem subDirFs) = CreateFileSystemInternal();

            subDirFs.CreateDirectory("/dir");

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/sub/path/dir"));
            Assert.Equal(DirectoryEntryType.Directory, type);
        }
    }

    public class SubdirectoryFileSystemTestsRoot : IFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            var baseFs = new InMemoryFileSystem();

            using var rootPath = new Path();
            PathFunctions.SetUpFixedPath(ref rootPath.Ref(), "/".ToU8String());

            var subFs = new SubdirectoryFileSystem(baseFs);
            subFs.Initialize(in rootPath).ThrowIfFailure();
            return subFs;
        }
    }
}
