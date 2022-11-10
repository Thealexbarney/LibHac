using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract partial class IFileSystemTests
{
    [Fact]
    public void CreateDirectory_EntryIsAdded()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");

        Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "/dir"));
        Assert.Equal(DirectoryEntryType.Directory, type);
    }

    [Fact]
    public void CreateDirectory_DirectoryExists_ReturnsPathAlreadyExists()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir");

        Result res = fs.CreateDirectory("/dir");

        Assert.Result(ResultFs.PathAlreadyExists, res);
    }

    [Fact]
    public void CreateDirectory_FileExists_ReturnsPathAlreadyExists()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file", 0, CreateFileOptions.None);

        Result res = fs.CreateDirectory("/file");

        Assert.Result(ResultFs.PathAlreadyExists, res);
    }

    [Fact]
    public void CreateDirectory_ParentDoesNotExist_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Result res = fs.CreateFile("/dir1/dir2", 0, CreateFileOptions.None);

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void CreateDirectory_WithTrailingSeparator_EntryIsAdded()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir/");
        Result res = fs.GetEntryType(out DirectoryEntryType type, "/dir/");

        Assert.Success(res);
        Assert.Equal(DirectoryEntryType.Directory, type);
    }

    [Fact]
    public void CreateDirectory_MultipleSiblings()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir1");
        fs.CreateDirectory("/dir2");

        Result result1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1");
        Result result2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2");

        Assert.Success(result1);
        Assert.Success(result2);
        Assert.Equal(DirectoryEntryType.Directory, type1);
        Assert.Equal(DirectoryEntryType.Directory, type2);
    }

    [Fact]
    public void CreateDirectory_InChildDirectory()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir1");
        fs.CreateDirectory("/dir2");

        fs.CreateDirectory("/dir1/dir1a");
        fs.CreateDirectory("/dir2/dir2a");

        Result result1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1/dir1a");
        Result result2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2/dir2a");

        Assert.Success(result1);
        Assert.Success(result2);
        Assert.Equal(DirectoryEntryType.Directory, type1);
        Assert.Equal(DirectoryEntryType.Directory, type2);
    }
}
