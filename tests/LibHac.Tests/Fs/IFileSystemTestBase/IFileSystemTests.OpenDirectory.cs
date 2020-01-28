using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void OpenDirectory_PathIsFile_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rc = fs.OpenDirectory(out _, "/file", OpenDirectoryMode.All);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }
    }
}