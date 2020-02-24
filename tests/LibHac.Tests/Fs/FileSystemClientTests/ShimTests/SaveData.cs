using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.Ncm;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests
{
    public class SaveData
    {
        [Fact]
        public void MountCacheStorage_CanMountCreatedCacheStorage()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId, 0, 0, SaveDataFlags.None);

            Assert.Success(fs.MountCacheStorage("cache".ToU8String(), applicationId));
        }

        [Fact]
        public void MountCacheStorage_WrittenDataPersists()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId, 0, 0, SaveDataFlags.None);
            fs.MountCacheStorage("cache".ToU8String(), applicationId);

            fs.CreateFile("cache:/file", 0);
            fs.Commit("cache");
            fs.Unmount("cache");

            Assert.Success(fs.MountCacheStorage("cache".ToU8String(), applicationId));
            Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "cache:/file"));
            Assert.Equal(DirectoryEntryType.File, type);
        }
        [Fact]
        public void MountCacheStorage_SdCardIsPreferredOverBis()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId, 0, 0, SaveDataFlags.None);
            fs.MountCacheStorage("cache".ToU8String(), applicationId);
            fs.CreateFile("cache:/sd", 0);
            fs.Commit("cache");
            fs.Unmount("cache");

            // Turn off the SD card so the User save is mounted
            fs.SetSdCardAccessibility(false);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId, 0, 0, SaveDataFlags.None);
            fs.MountCacheStorage("cache".ToU8String(), applicationId);
            fs.CreateFile("cache:/bis", 0);
            fs.Commit("cache");
            fs.Unmount("cache");

            fs.SetSdCardAccessibility(true);

            Assert.Success(fs.MountCacheStorage("cache".ToU8String(), applicationId));
            Assert.Success(fs.GetEntryType(out _, "cache:/sd"));
            Assert.Failure(fs.GetEntryType(out _, "cache:/bis"));
        }
    }
}
