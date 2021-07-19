using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void RenameDirectory_EntriesAreMoved()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1");
            Result rcRename = fs.RenameDirectory("/dir1", "/dir2");

            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");
            Result rcDir1 = fs.GetEntryType(out _, "/dir1");

            Assert.Success(rcRename);

            Assert.Success(rcDir2);
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);

            Assert.Result(ResultFs.PathNotFound, rcDir1);
        }

        [Fact]
        public void RenameDirectory_HasChildren_NewChildPathExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir1/dirC");
            fs.CreateFile("/dir1/file1", 0, CreateFileOptions.None);

            Result rcRename = fs.RenameDirectory("/dir1", "/dir2");

            // Check that renamed structure exists
            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");
            Result rcDirC = fs.GetEntryType(out DirectoryEntryType dir1CType, "/dir2/dirC");
            Result rcFile1 = fs.GetEntryType(out DirectoryEntryType file1Type, "/dir2/file1");

            // Check that old structure doesn't exist
            Result rcDir1 = fs.GetEntryType(out _, "/dir1");
            Result rcDirCOld = fs.GetEntryType(out _, "/dir1/dirC");
            Result rcFile1Old = fs.GetEntryType(out _, "/dir1/file1");

            Assert.Success(rcRename);

            Assert.Success(rcDir2);
            Assert.Success(rcDirC);
            Assert.Success(rcFile1);

            Assert.Equal(DirectoryEntryType.Directory, dir2Type);
            Assert.Equal(DirectoryEntryType.Directory, dir1CType);
            Assert.Equal(DirectoryEntryType.File, file1Type);

            Assert.Result(ResultFs.PathNotFound, rcDir1);
            Assert.Result(ResultFs.PathNotFound, rcDirCOld);
            Assert.Result(ResultFs.PathNotFound, rcFile1Old);
        }

        [Fact]
        public void RenameDirectory_DestHasDifferentParentDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/parent1");
            fs.CreateDirectory("/parent2");
            fs.CreateDirectory("/parent1/dir1");

            Result rcRename = fs.RenameDirectory("/parent1/dir1", "/parent2/dir2");

            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/parent2/dir2");
            Result rcDir1 = fs.GetEntryType(out _, "/parent1/dir1");

            Assert.Success(rcRename);

            Assert.Equal(Result.Success, rcDir2);
            Assert.Success(rcDir2);
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);

            Assert.Result(ResultFs.PathNotFound, rcDir1);
        }

        [Fact]
        public void RenameDirectory_DestExists_ReturnsPathAlreadyExists()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir2");

            Result rcRename = fs.RenameDirectory("/dir1", "/dir2");

            Result rcDir1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");

            Assert.Result(ResultFs.PathAlreadyExists, rcRename);

            Assert.Success(rcDir1);
            Assert.Success(rcDir2);
            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);
        }
    }
}