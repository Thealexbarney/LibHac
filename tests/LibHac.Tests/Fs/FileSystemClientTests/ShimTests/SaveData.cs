using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests;

public class SaveData
{
    [Fact]
    public void MountCacheStorage_CanMountCreatedCacheStorage()
    {
        var applicationId = new Ncm.ApplicationId(1);
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, SaveDataFlags.None);

        Assert.Success(fs.MountCacheStorage("cache"u8, applicationId));
    }

    [Fact]
    public void MountCacheStorage_WrittenDataPersists()
    {
        var applicationId = new Ncm.ApplicationId(1);
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdUser, applicationId.Value, 0, 0, SaveDataFlags.None);
        fs.MountCacheStorage("cache"u8, applicationId);

        fs.CreateFile("cache:/file"u8, 0);
        fs.Commit("cache"u8);
        fs.Unmount("cache"u8);

        Assert.Success(fs.MountCacheStorage("cache"u8, applicationId));
        Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "cache:/file"u8));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void MountCacheStorage_SdCardIsPreferredOverBis()
    {
        var applicationId = new Ncm.ApplicationId(1);
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdUser, applicationId.Value, 0, 0, SaveDataFlags.None));
        Assert.Success(fs.MountCacheStorage("cache"u8, applicationId));
        fs.CreateFile("cache:/sd"u8, 0);
        fs.Commit("cache"u8);
        fs.Unmount("cache"u8);

        // Turn off the SD card so the User save is mounted
        fs.SetSdCardAccessibility(false);

        fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, SaveDataFlags.None);
        fs.MountCacheStorage("cache"u8, applicationId);
        fs.CreateFile("cache:/bis"u8, 0);
        fs.Commit("cache"u8);
        fs.Unmount("cache"u8);

        fs.SetSdCardAccessibility(true);

        Assert.Success(fs.MountCacheStorage("cache"u8, applicationId));
        Assert.Success(fs.GetEntryType(out _, "cache:/sd"u8));
        Assert.Failure(fs.GetEntryType(out _, "cache:/bis"u8));
    }
}