using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests
{
    public class SaveData
    {
        [Fact]
        public void MountCacheStorage_CanMountCreatedCacheStorage()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, SaveDataFlags.None);

            Assert.Success(fs.MountCacheStorage("cache".ToU8Span(), applicationId));
        }

        [Fact]
        public void MountCacheStorage_WrittenDataPersists()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId.Value, 0, 0, SaveDataFlags.None);
            fs.MountCacheStorage("cache".ToU8Span(), applicationId);

            fs.CreateFile("cache:/file".ToU8Span(), 0);
            fs.Commit("cache".ToU8Span());
            fs.Unmount("cache".ToU8Span());

            Assert.Success(fs.MountCacheStorage("cache".ToU8Span(), applicationId));
            Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "cache:/file".ToU8Span()));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void MountCacheStorage_SdCardIsPreferredOverBis()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId.Value, 0, 0, SaveDataFlags.None);
            fs.MountCacheStorage("cache".ToU8Span(), applicationId);
            fs.CreateFile("cache:/sd".ToU8Span(), 0);
            fs.Commit("cache".ToU8Span());
            fs.Unmount("cache".ToU8Span());

            // Turn off the SD card so the User save is mounted
            fs.SetSdCardAccessibility(false);

            fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, SaveDataFlags.None);
            fs.MountCacheStorage("cache".ToU8Span(), applicationId);
            fs.CreateFile("cache:/bis".ToU8Span(), 0);
            fs.Commit("cache".ToU8Span());
            fs.Unmount("cache".ToU8Span());

            fs.SetSdCardAccessibility(true);

            Assert.Success(fs.MountCacheStorage("cache".ToU8String(), applicationId));
            Assert.Success(fs.GetEntryType(out _, "cache:/sd".ToU8Span()));
            Assert.Failure(fs.GetEntryType(out _, "cache:/bis".ToU8Span()));
        }
    }
}
