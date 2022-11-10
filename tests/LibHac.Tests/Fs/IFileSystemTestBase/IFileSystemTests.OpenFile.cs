using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void OpenFile_PathIsDirectory_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");

        using var file = new UniqueRef<IFile>();
        Result res = fs.OpenFile(ref file.Ref(), "/dir", OpenMode.All);

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void OpenFile_PathDoesNotExist_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        using var file = new UniqueRef<IFile>();
        Result res = fs.OpenFile(ref file.Ref(), "/file", OpenMode.All);

        Assert.Result(ResultFs.PathNotFound, res);
    }
}