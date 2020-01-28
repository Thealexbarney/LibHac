using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void GetEntryType_RootIsDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type);
        }

        [Fact]
        public void GetEntryType_PathDoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Result rc = fs.GetEntryType(out _, "/path");

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }
    }
}