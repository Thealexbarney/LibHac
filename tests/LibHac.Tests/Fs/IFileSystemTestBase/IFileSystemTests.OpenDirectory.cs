using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void OpenDirectory_PathIsFile_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            Result rc = fs.OpenDirectory(out _, "/file".ToU8Span(), OpenDirectoryMode.All);

            Assert.Result(ResultFs.PathNotFound, rc);
        }
    }
}