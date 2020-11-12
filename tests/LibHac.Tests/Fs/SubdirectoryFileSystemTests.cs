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
            baseFs.CreateDirectory("/sub".ToU8Span());
            baseFs.CreateDirectory("/sub/path".ToU8Span());

            var subFs = new SubdirectoryFileSystem(baseFs);
            subFs.Initialize("/sub/path".ToU8String()).ThrowIfFailure();

            return (baseFs, subFs);
        }

        [Fact]
        public void CreateFile_CreatedInBaseFileSystem()
        {
            (IFileSystem baseFs, IFileSystem subDirFs) = CreateFileSystemInternal();

            subDirFs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/sub/path/file".ToU8Span()));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateDirectory_CreatedInBaseFileSystem()
        {
            (IFileSystem baseFs, IFileSystem subDirFs) = CreateFileSystemInternal();

            subDirFs.CreateDirectory("/dir".ToU8Span());

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/sub/path/dir".ToU8Span()));
            Assert.Equal(DirectoryEntryType.Directory, type);
        }
    }

    public class SubdirectoryFileSystemTestsRoot : IFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            var baseFs = new InMemoryFileSystem();

            var subFs = new SubdirectoryFileSystem(baseFs);
            subFs.Initialize("/".ToU8String()).ThrowIfFailure();
            return subFs;
        }
    }
}
