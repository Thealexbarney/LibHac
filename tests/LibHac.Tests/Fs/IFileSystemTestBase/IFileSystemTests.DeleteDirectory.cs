using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void DeleteDirectory_DoesNotExist_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Result res = fs.DeleteDirectory("/dir");

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void DeleteDirectory_DirectoryExists_EntryIsRemoved()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");

        Result resultDelete = fs.DeleteDirectory("/dir");
        Result resultEntry = fs.GetEntryType(out _, "/dir");

        Assert.Success(resultDelete);
        Assert.Result(ResultFs.PathNotFound, resultEntry);
    }

    [Fact]
    public void DeleteDirectory_PathIsFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file", 0, CreateFileOptions.None);

        Result res = fs.DeleteDirectory("/file");

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void DeleteDirectory_HasOlderSibling_SiblingNotDeleted()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir1");
        fs.CreateDirectory("/dir2");

        Result resultDelete = fs.DeleteDirectory("/dir2");
        Result resultEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
        Result resultEntry2 = fs.GetEntryType(out _, "/dir2");

        Assert.Success(resultDelete);
        Assert.Success(resultEntry1);
        Assert.Result(ResultFs.PathNotFound, resultEntry2);

        Assert.Equal(DirectoryEntryType.Directory, dir1Type);
    }

    [Fact]
    public void DeleteDirectory_HasYoungerSibling_SiblingNotDeleted()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir2");
        fs.CreateDirectory("/dir1");

        Result resultDelete = fs.DeleteDirectory("/dir2");
        Result resultEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
        Result resultEntry2 = fs.GetEntryType(out _, "/dir2");

        Assert.Success(resultDelete);
        Assert.Success(resultEntry1);
        Assert.Result(ResultFs.PathNotFound, resultEntry2);

        Assert.Equal(DirectoryEntryType.Directory, dir1Type);
    }

    [Fact]
    public void DeleteDirectory_NotEmpty_ReturnsDirectoryNotEmpty()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");
        fs.CreateFile("/dir/file", 0, CreateFileOptions.None);

        Result res = fs.DeleteDirectory("/dir");

        Assert.Result(ResultFs.DirectoryNotEmpty, res);
    }
}