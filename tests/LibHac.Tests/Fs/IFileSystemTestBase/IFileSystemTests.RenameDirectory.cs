using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void RenameDirectory_EntriesAreMoved()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1".ToU8Span());
            Result rcRename = fs.RenameDirectory("/dir1".ToU8Span(), "/dir2".ToU8Span());

            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2".ToU8Span());
            Result rcDir1 = fs.GetEntryType(out _, "/dir1".ToU8Span());

            Assert.True(rcRename.IsSuccess());

            Assert.True(rcDir2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1);
        }

        [Fact]
        public void RenameDirectory_HasChildren_NewChildPathExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1".ToU8Span());
            fs.CreateDirectory("/dir1/dirC".ToU8Span());
            fs.CreateFile("/dir1/file1".ToU8Span(), 0, CreateFileOptions.None);

            Result rcRename = fs.RenameDirectory("/dir1".ToU8Span(), "/dir2".ToU8Span());

            // Check that renamed structure exists
            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2".ToU8Span());
            Result rcDirC = fs.GetEntryType(out DirectoryEntryType dir1CType, "/dir2/dirC".ToU8Span());
            Result rcFile1 = fs.GetEntryType(out DirectoryEntryType file1Type, "/dir2/file1".ToU8Span());

            // Check that old structure doesn't exist
            Result rcDir1 = fs.GetEntryType(out _, "/dir1".ToU8Span());
            Result rcDirCOld = fs.GetEntryType(out _, "/dir1/dirC".ToU8Span());
            Result rcFile1Old = fs.GetEntryType(out _, "/dir1/file1".ToU8Span());

            Assert.True(rcRename.IsSuccess());

            Assert.True(rcDir2.IsSuccess());
            Assert.True(rcDirC.IsSuccess());
            Assert.True(rcFile1.IsSuccess());

            Assert.Equal(DirectoryEntryType.Directory, dir2Type);
            Assert.Equal(DirectoryEntryType.Directory, dir1CType);
            Assert.Equal(DirectoryEntryType.File, file1Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1);
            Assert.Equal(ResultFs.PathNotFound.Value, rcDirCOld);
            Assert.Equal(ResultFs.PathNotFound.Value, rcFile1Old);
        }

        [Fact]
        public void RenameDirectory_DestHasDifferentParentDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/parent1".ToU8Span());
            fs.CreateDirectory("/parent2".ToU8Span());
            fs.CreateDirectory("/parent1/dir1".ToU8Span());

            Result rcRename = fs.RenameDirectory("/parent1/dir1".ToU8Span(), "/parent2/dir2".ToU8Span());

            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/parent2/dir2".ToU8Span());
            Result rcDir1 = fs.GetEntryType(out _, "/parent1/dir1".ToU8Span());

            Assert.True(rcRename.IsSuccess());

            Assert.Equal(Result.Success, rcDir2);
            Assert.True(rcDir2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1);
        }

        [Fact]
        public void RenameDirectory_DestExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1".ToU8Span());
            fs.CreateDirectory("/dir2".ToU8Span());

            Result rcRename = fs.RenameDirectory("/dir1".ToU8Span(), "/dir2".ToU8Span());

            Result rcDir1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1".ToU8Span());
            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2".ToU8Span());

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rcRename);

            Assert.True(rcDir1.IsSuccess());
            Assert.True(rcDir2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);
        }
    }
}