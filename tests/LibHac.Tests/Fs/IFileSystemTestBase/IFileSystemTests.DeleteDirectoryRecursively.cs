using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void DeleteDirectoryRecursively_DeletesDirectoryAndChildren()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir");
            fs.CreateDirectory("/dir/dir2");
            fs.CreateFile("/dir/file1", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteDirectoryRecursively("/dir");

            Result rcDir1Type = fs.GetEntryType(out _, "/dir");
            Result rcDir2Type = fs.GetEntryType(out _, "/dir/dir2");
            Result rcFileType = fs.GetEntryType(out _, "/dir/file1");

            Assert.True(rcDelete.IsSuccess());

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcDir2Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcFileType);
        }
    }
}