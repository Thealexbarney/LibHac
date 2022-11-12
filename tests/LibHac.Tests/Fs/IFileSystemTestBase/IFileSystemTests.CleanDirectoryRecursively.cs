using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void CleanDirectoryRecursively_DeletesChildren()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");
        fs.CreateDirectory("/dir/dir2");
        fs.CreateFile("/dir/file1", 0, CreateFileOptions.None);

        Result resultDelete = fs.CleanDirectoryRecursively("/dir");

        Result resultDir1Type = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir");
        Result resultDir2Type = fs.GetEntryType(out _, "/dir/dir2");
        Result resultFileType = fs.GetEntryType(out _, "/dir/file1");

        Assert.Success(resultDelete);

        Assert.Success(resultDir1Type);
        Assert.Equal(DirectoryEntryType.Directory, dir1Type);

        Assert.Result(ResultFs.PathNotFound, resultDir2Type);
        Assert.Result(ResultFs.PathNotFound, resultFileType);
    }
}