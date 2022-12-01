using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests;

public class BcatSaveData
{
    [Fact]
    public void MountBcatSaveData_SaveDoesNotExist_ReturnsTargetNotFound()
    {
        var applicationId = new Ncm.ApplicationId(1);
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Result(ResultFs.TargetNotFound, fs.MountBcatSaveData("bcat_test"u8, applicationId));
    }

    [Fact]
    public void MountBcatSaveData_SaveExists_ReturnsSuccess()
    {
        var applicationId = new Ncm.ApplicationId(1);
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.CreateBcatSaveData(applicationId, 0x400000));
        Assert.Success(fs.MountBcatSaveData("bcat_test"u8, applicationId));
    }

    [Fact]
    public void MountBcatSaveData_WrittenDataPersists()
    {
        var applicationId = new Ncm.ApplicationId(1);
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.CreateBcatSaveData(applicationId, 0x400000));
        Assert.Success(fs.MountBcatSaveData("bcat_test"u8, applicationId));

        // Check that the path doesn't exist
        Assert.Result(ResultFs.PathNotFound, fs.GetEntryType(out _, "bcat_test:/file"u8));

        fs.CreateFile("bcat_test:/file"u8, 0);
        fs.Commit("bcat_test"u8);
        fs.Unmount("bcat_test"u8);

        Assert.Success(fs.MountBcatSaveData("bcat_test"u8, applicationId));
        Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "bcat_test:/file"u8));
        Assert.Equal(DirectoryEntryType.File, type);
    }
}