using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void SetSize_FileSizeModified()
        {
            IFileSystem fs = CreateFileSystem();
            fs.CreateFile("/file", 0, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file", OpenMode.All);
            Result rc = file.SetSize(54321);
            file.Dispose();

            fs.OpenFile(out file, "/file", OpenMode.All);
            file.GetSize(out long fileSize);
            file.Dispose();

            Assert.Success(rc);
            Assert.Equal(54321, fileSize);
        }
    }
}