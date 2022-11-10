using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase;

public abstract class IAttributeFileSystemTests : IFileSystemTests
{
    protected abstract IAttributeFileSystem CreateAttributeFileSystem();

    [Fact]
    public void CreateDirectory_WithoutArchiveAttribute_ArchiveFlagIsNotSet()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();

        Assert.Success(fs.CreateDirectory("/dir", NxFileAttributes.None));

        Assert.Success(fs.GetFileAttributes(out NxFileAttributes attributes, "/dir"));
        Assert.Equal(NxFileAttributes.Directory, attributes);
    }

    [Fact]
    public void CreateDirectory_WithArchiveAttribute_ArchiveFlagIsSet()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();

        Assert.Success(fs.CreateDirectory("/dir", NxFileAttributes.Archive));

        Assert.Success(fs.GetFileAttributes(out NxFileAttributes attributes, "/dir"));
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, attributes);
    }

    [Fact]
    public void GetFileAttributes_AttributesOnNewFileAreEmpty()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();
        fs.CreateFile("/file", 0, CreateFileOptions.None);

        Assert.Success(fs.GetFileAttributes(out NxFileAttributes attributes, "/file"));
        Assert.Equal(NxFileAttributes.None, attributes);
    }

    [Fact]
    public void GetFileAttributes_AttributesOnNewDirHaveOnlyDirFlagSet()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();
        fs.CreateDirectory("/dir");

        Assert.Success(fs.GetFileAttributes(out NxFileAttributes attributes, "/dir"));
        Assert.Equal(NxFileAttributes.Directory, attributes);
    }

    [Fact]
    public void GetFileAttributes_PathDoesNotExist_ReturnsPathNotFound()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();

        Result res = fs.GetFileAttributes(out _, "/path");

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void SetFileAttributes_PathDoesNotExist_ReturnsPathNotFound()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();

        Result res = fs.SetFileAttributes("/path", NxFileAttributes.None);

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void SetFileAttributes_SetAttributeOnFile()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();
        fs.CreateFile("/file", 0, CreateFileOptions.None);

        Result resultSet = fs.SetFileAttributes("/file", NxFileAttributes.Archive);
        Result resultGet = fs.GetFileAttributes(out NxFileAttributes attributes, "/file");

        Assert.Success(resultSet);
        Assert.Success(resultGet);
        Assert.Equal(NxFileAttributes.Archive, attributes);
    }

    [Fact]
    public void SetFileAttributes_SetAttributeOnDirectory()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();
        fs.CreateDirectory("/dir");

        Result resultSet = fs.SetFileAttributes("/dir", NxFileAttributes.Archive);
        Result resultGet = fs.GetFileAttributes(out NxFileAttributes attributes, "/dir");

        Assert.Success(resultSet);
        Assert.Success(resultGet);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, attributes);
    }

    [Fact]
    public void GetFileSize_ReadNewFileSize()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();

        fs.CreateFile("/file", 845, CreateFileOptions.None);

        Assert.Success(fs.GetFileSize(out long fileSize, "/file"));
        Assert.Equal(845, fileSize);
    }

    [Fact]
    public void GetFileSize_PathDoesNotExist_ReturnsPathNotFound()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();

        Result res = fs.GetFileSize(out _, "/path");

        Assert.Result(ResultFs.PathNotFound, res);
    }

    [Fact]
    public void GetFileSize_PathIsDirectory_ReturnsPathNotFound()
    {
        IAttributeFileSystem fs = CreateAttributeFileSystem();
        fs.CreateDirectory("/dir");

        Result res = fs.GetFileSize(out _, "/dir");

        Assert.Result(ResultFs.PathNotFound, res);
    }
}
