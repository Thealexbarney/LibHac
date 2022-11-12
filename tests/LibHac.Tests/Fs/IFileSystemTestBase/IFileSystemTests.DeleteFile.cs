using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void DeleteFile_DoesNotExist_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Result res = fs.DeleteFile("/file");
        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void DeleteFile_FileExists_FileEntryIsRemoved()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file", 0, CreateFileOptions.None);

        Result resultDelete = fs.DeleteFile("/file");
        Result resultEntry = fs.GetEntryType(out _, "/file");

        Assert.Success(resultDelete);
        Assert.Result(ResultFs.PathNotFound, resultEntry);
    }

    [Fact]
    public void DeleteFile_PathIsDirectory_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");

        Result res = fs.DeleteFile("/dir");

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void DeleteFile_HasOlderSibling_SiblingNotDeleted()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file1", 0, CreateFileOptions.None);
        fs.CreateFile("/file2", 0, CreateFileOptions.None);

        Result resultDelete = fs.DeleteFile("/file2");
        Result resultEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/file1");
        Result resultEntry2 = fs.GetEntryType(out _, "/file2");

        Assert.Success(resultDelete);
        Assert.Success(resultEntry1);
        Assert.Result(ResultFs.PathNotFound, resultEntry2);

        Assert.Equal(DirectoryEntryType.File, dir1Type);
    }

    [Fact]
    public void DeleteFile_HasYoungerSibling_SiblingNotDeleted()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file2", 0, CreateFileOptions.None);
        fs.CreateFile("/file1", 0, CreateFileOptions.None);

        Result resultDelete = fs.DeleteFile("/file2");
        Result resultEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/file1");
        Result resultEntry2 = fs.GetEntryType(out _, "/file2");

        Assert.Success(resultDelete);
        Assert.Success(resultEntry1);
        Assert.Result(ResultFs.PathNotFound, resultEntry2);

        Assert.Equal(DirectoryEntryType.File, dir1Type);
    }
}