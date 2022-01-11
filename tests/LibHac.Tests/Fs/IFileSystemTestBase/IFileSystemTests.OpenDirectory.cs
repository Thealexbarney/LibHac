using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void OpenDirectory_PathIsFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file", 0, CreateFileOptions.None);

        using var directory = new UniqueRef<IDirectory>();
        Result rc = fs.OpenDirectory(ref directory.Ref(), "/file", OpenDirectoryMode.All);

        Assert.Result(ResultFs.PathNotFound, rc);
    }

    [Fact]
    public void OpenDirectory_PathDoesNotExist_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        using var directory = new UniqueRef<IDirectory>();
        Result rc = fs.OpenDirectory(ref directory.Ref(), "/dir", OpenDirectoryMode.All);

        Assert.Result(ResultFs.PathNotFound, rc);
    }
}