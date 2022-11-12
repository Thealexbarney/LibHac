using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void RenameDirectory_EntriesAreMoved()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir1");
        Result resultRename = fs.RenameDirectory("/dir1", "/dir2");

        Result resultDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");
        Result resultDir1 = fs.GetEntryType(out _, "/dir1");

        Assert.Success(resultRename);

        Assert.Success(resultDir2);
        Assert.Equal(DirectoryEntryType.Directory, dir2Type);

        Assert.Result(ResultFs.PathNotFound, resultDir1);
    }

    [Fact]
    public void RenameDirectory_HasChildren_NewChildPathExists()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir1");
        fs.CreateDirectory("/dir1/dirC");
        fs.CreateFile("/dir1/file1", 0, CreateFileOptions.None);

        Result resultRename = fs.RenameDirectory("/dir1", "/dir2");

        // Check that renamed structure exists
        Result resultDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");
        Result resultDirC = fs.GetEntryType(out DirectoryEntryType dir1CType, "/dir2/dirC");
        Result resultFile1 = fs.GetEntryType(out DirectoryEntryType file1Type, "/dir2/file1");

        // Check that old structure doesn't exist
        Result resultDir1 = fs.GetEntryType(out _, "/dir1");
        Result resultDirCOld = fs.GetEntryType(out _, "/dir1/dirC");
        Result resultFile1Old = fs.GetEntryType(out _, "/dir1/file1");

        Assert.Success(resultRename);

        Assert.Success(resultDir2);
        Assert.Success(resultDirC);
        Assert.Success(resultFile1);

        Assert.Equal(DirectoryEntryType.Directory, dir2Type);
        Assert.Equal(DirectoryEntryType.Directory, dir1CType);
        Assert.Equal(DirectoryEntryType.File, file1Type);

        Assert.Result(ResultFs.PathNotFound, resultDir1);
        Assert.Result(ResultFs.PathNotFound, resultDirCOld);
        Assert.Result(ResultFs.PathNotFound, resultFile1Old);
    }

    [Fact]
    public void RenameDirectory_DestHasDifferentParentDirectory()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/parent1");
        fs.CreateDirectory("/parent2");
        fs.CreateDirectory("/parent1/dir1");

        Result resultRename = fs.RenameDirectory("/parent1/dir1", "/parent2/dir2");

        Result resultDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/parent2/dir2");
        Result resultDir1 = fs.GetEntryType(out _, "/parent1/dir1");

        Assert.Success(resultRename);

        Assert.Equal(Result.Success, resultDir2);
        Assert.Success(resultDir2);
        Assert.Equal(DirectoryEntryType.Directory, dir2Type);

        Assert.Result(ResultFs.PathNotFound, resultDir1);
    }

    [Fact]
    public void RenameDirectory_DestExists_ReturnsPathAlreadyExists()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir1");
        fs.CreateDirectory("/dir2");

        Result resultRename = fs.RenameDirectory("/dir1", "/dir2");

        Result resultDir1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
        Result resultDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");

        Assert.Result(ResultFs.PathAlreadyExists, resultRename);

        Assert.Success(resultDir1);
        Assert.Success(resultDir2);
        Assert.Equal(DirectoryEntryType.Directory, dir1Type);
        Assert.Equal(DirectoryEntryType.Directory, dir2Type);
    }
}