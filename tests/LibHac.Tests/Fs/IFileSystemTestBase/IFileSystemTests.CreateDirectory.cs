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

        Result rc = fs.CreateDirectory("/dir");

        Assert.Result(ResultFs.PathAlreadyExists, rc);
    }

    [Fact]
    public void CreateDirectory_FileExists_ReturnsPathAlreadyExists()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file", 0, CreateFileOptions.None);

        Result rc = fs.CreateDirectory("/file");

        Assert.Result(ResultFs.PathAlreadyExists, rc);
    }

    [Fact]
    public void CreateDirectory_ParentDoesNotExist_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Result rc = fs.CreateFile("/dir1/dir2", 0, CreateFileOptions.None);

        Assert.Result(ResultFs.PathNotFound, rc);
    }

    [Fact]
    public void CreateDirectory_WithTrailingSeparator_EntryIsAdded()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir/");
        Result rc = fs.GetEntryType(out DirectoryEntryType type, "/dir/");

        Assert.Success(rc);
        Assert.Equal(DirectoryEntryType.Directory, type);
    }

    [Fact]
    public void CreateDirectory_MultipleSiblings()
    {
        IFileSystem fs = CreateFileSystem();

        fs.CreateDirectory("/dir1");
        fs.CreateDirectory("/dir2");

        Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1");
        Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2");

        Assert.Success(rc1);
        Assert.Success(rc2);
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

        Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1/dir1a");
        Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2/dir2a");

        Assert.Success(rc1);
        Assert.Success(rc2);
        Assert.Equal(DirectoryEntryType.Directory, type1);
        Assert.Equal(DirectoryEntryType.Directory, type2);
    }
}
