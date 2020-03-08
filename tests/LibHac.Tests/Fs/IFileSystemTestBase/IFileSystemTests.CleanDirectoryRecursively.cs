using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void CleanDirectoryRecursively_DeletesChildren()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir".ToU8Span());
            fs.CreateDirectory("/dir/dir2".ToU8Span());
            fs.CreateFile("/dir/file1".ToU8Span(), 0, CreateFileOptions.None);

            Result rcDelete = fs.CleanDirectoryRecursively("/dir".ToU8Span());

            Result rcDir1Type = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir".ToU8Span());
            Result rcDir2Type = fs.GetEntryType(out _, "/dir/dir2".ToU8Span());
            Result rcFileType = fs.GetEntryType(out _, "/dir/file1".ToU8Span());

            Assert.True(rcDelete.IsSuccess());

            Assert.True(rcDir1Type.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir1Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir2Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcFileType);
        }
    }
}