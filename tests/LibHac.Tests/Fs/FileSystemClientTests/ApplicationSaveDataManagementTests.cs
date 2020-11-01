using LibHac.Account;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.Ns;
using Xunit;

using static LibHac.Fs.ApplicationSaveDataManagement;

namespace LibHac.Tests.Fs.FileSystemClientTests
{
    public class ApplicationSaveDataManagementTests
    {
        [Fact]
        public static void EnsureApplicationSaveData_CreatesAccountSaveData()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            var applicationId = new Ncm.ApplicationId(11);
            var userId = new Uid(2, 3);

            var nacp = new ApplicationControlProperty
            {
                UserAccountSaveDataSize = 0x1000,
                UserAccountSaveDataJournalSize = 0x1000
            };

            Assert.Success(EnsureApplicationSaveData(fs, out _, applicationId, ref nacp, ref userId));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            Assert.Success(iterator.ReadSaveDataInfo(out long entriesRead, info));

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(ConvertAccountUidToFsUserId(userId), info[0].UserId);
            Assert.Equal(SaveDataType.Account, info[0].Type);
        }

        [Fact]
        public static void EnsureApplicationSaveData_CreatesDeviceSaveData()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            var applicationId = new Ncm.ApplicationId(11);
            var userId = new Uid(2, 3);

            var nacp = new ApplicationControlProperty
            {
                DeviceSaveDataSize = 0x1000,
                DeviceSaveDataJournalSize = 0x1000
            };

            Assert.Success(EnsureApplicationSaveData(fs, out _, applicationId, ref nacp, ref userId));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            Assert.Success(iterator.ReadSaveDataInfo(out long entriesRead, info));

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(UserId.InvalidId, info[0].UserId);
            Assert.Equal(SaveDataType.Device, info[0].Type);
        }

        [Fact]
        public static void EnsureApplicationSaveData_CreatesBcatCacheStorage()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            var applicationId = new Ncm.ApplicationId(11);
            var userId = new Uid(2, 3);

            var nacp = new ApplicationControlProperty
            {
                BcatDeliveryCacheStorageSize = 0x1000
            };

            Assert.Success(EnsureApplicationSaveData(fs, out _, applicationId, ref nacp, ref userId));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            Assert.Success(iterator.ReadSaveDataInfo(out long entriesRead, info));

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(UserId.InvalidId, info[0].UserId);
            Assert.Equal(SaveDataType.Bcat, info[0].Type);
        }

        [Fact]
        public static void EnsureApplicationSaveData_CreatesTemporaryStorage()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            var applicationId = new Ncm.ApplicationId(11);
            var userId = new Uid(2, 3);

            var nacp = new ApplicationControlProperty
            {
                TemporaryStorageSize = 0x1000
            };

            Assert.Success(EnsureApplicationSaveData(fs, out _, applicationId, ref nacp, ref userId));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.Temporary);

            var info = new SaveDataInfo[2];
            Assert.Success(iterator.ReadSaveDataInfo(out long entriesRead, info));

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(UserId.InvalidId, info[0].UserId);
            Assert.Equal(SaveDataType.Temporary, info[0].Type);
        }

        [Fact]
        public static void EnsureApplicationCacheStorage_SdCardAvailable_CreatesCacheStorageOnSd()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            var applicationId = new Ncm.ApplicationId(11);

            var nacp = new ApplicationControlProperty
            {
                CacheStorageSize = 0x1000,
                CacheStorageJournalSize = 0x1000
            };

            Assert.Success(fs.EnsureApplicationCacheStorage(out _, out CacheStorageTargetMedia target, applicationId,
                ref nacp));

            Assert.Equal(CacheStorageTargetMedia.SdCard, target);

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.SdCache);

            var info = new SaveDataInfo[2];
            Assert.Success(iterator.ReadSaveDataInfo(out long entriesRead, info));

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(SaveDataType.Cache, info[0].Type);
        }

        [Fact]
        public static void EnsureApplicationCacheStorage_SdCardNotAvailable_CreatesCacheStorageOnBis()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

            var applicationId = new Ncm.ApplicationId(11);

            var nacp = new ApplicationControlProperty
            {
                CacheStorageSize = 0x1000,
                CacheStorageJournalSize = 0x1000
            };

            Assert.Success(fs.EnsureApplicationCacheStorage(out _, out CacheStorageTargetMedia target, applicationId,
                ref nacp));

            Assert.Equal(CacheStorageTargetMedia.Nand, target);

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            Assert.Success(iterator.ReadSaveDataInfo(out long entriesRead, info));

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(SaveDataType.Cache, info[0].Type);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public static void GetCacheStorageTargetMedia_ReturnsTargetOfNewCacheStorage(bool isSdCardInserted)
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(isSdCardInserted);

            var applicationId = new Ncm.ApplicationId(11);

            var nacp = new ApplicationControlProperty
            {
                CacheStorageSize = 0x1000,
                CacheStorageJournalSize = 0x1000
            };

            fs.EnsureApplicationCacheStorage(out _, out CacheStorageTargetMedia targetFromCreation, applicationId, ref nacp);

            Assert.Success(fs.GetCacheStorageTargetMedia(out CacheStorageTargetMedia target, applicationId));
            Assert.Equal(targetFromCreation, target);
        }

        [Fact]
        public static void GetCacheStorageTargetMedia_CacheStorageDoesNotExist_ReturnsNone()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.GetCacheStorageTargetMedia(out CacheStorageTargetMedia target, new Ncm.ApplicationId(11)));
            Assert.Equal(CacheStorageTargetMedia.None, target);
        }
    }
}
