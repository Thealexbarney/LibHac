using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void OpenFile_PathIsDirectory_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir");

            Result rc = fs.OpenFile(out _, "/dir", OpenMode.All);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }
    }
}