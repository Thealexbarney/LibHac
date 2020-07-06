using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void OpenFile_PathIsDirectory_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());

            Result rc = fs.OpenFile(out _, "/dir".ToU8Span(), OpenMode.All);

            Assert.Result(ResultFs.PathNotFound, rc);
        }
    }
}