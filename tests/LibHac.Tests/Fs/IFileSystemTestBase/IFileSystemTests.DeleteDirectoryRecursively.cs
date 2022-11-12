using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void DeleteDirectoryRecursively_DeletesDirectoryAndChildren()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");
        fs.CreateDirectory("/dir/dir2");
        fs.CreateFile("/dir/file1", 0, CreateFileOptions.None);

        Result resultDelete = fs.DeleteDirectoryRecursively("/dir");

        Result resultDir1Type = fs.GetEntryType(out _, "/dir");
        Result resultDir2Type = fs.GetEntryType(out _, "/dir/dir2");
        Result resultFileType = fs.GetEntryType(out _, "/dir/file1");

        Assert.Success(resultDelete);

        Assert.Result(ResultFs.PathNotFound, resultDir1Type);
        Assert.Result(ResultFs.PathNotFound, resultDir2Type);
        Assert.Result(ResultFs.PathNotFound, resultFileType);
    }
}