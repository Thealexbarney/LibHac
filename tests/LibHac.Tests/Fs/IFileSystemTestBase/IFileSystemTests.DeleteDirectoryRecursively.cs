using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void DeleteDirectoryRecursively_DeletesDirectoryAndChildren()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());
            fs.CreateDirectory("/dir/dir2".ToU8Span());
            fs.CreateFile("/dir/file1".ToU8Span(), 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteDirectoryRecursively("/dir".ToU8Span());

            Result rcDir1Type = fs.GetEntryType(out _, "/dir".ToU8Span());
            Result rcDir2Type = fs.GetEntryType(out _, "/dir/dir2".ToU8Span());
            Result rcFileType = fs.GetEntryType(out _, "/dir/file1".ToU8Span());

            Assert.True(rcDelete.IsSuccess());

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcDir2Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcFileType);
        }
    }
}