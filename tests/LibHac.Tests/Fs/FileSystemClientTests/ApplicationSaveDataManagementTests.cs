using LibHac.Account;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.Ns;
using Xunit;

using static LibHac.Fs.SaveData;

namespace LibHac.Tests.Fs.FileSystemClientTests;

public class ApplicationSaveDataManagementTests
{
    [Fact]
    public static void EnsureApplicationSaveData_CreatesAccountSaveData()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        var applicationId = new Ncm.ApplicationId(11);
        var userId = new Uid(2, 3);

        var controlProperty = new ApplicationControlProperty
        {
            UserAccountSaveDataSize = 0x1000,
            UserAccountSaveDataJournalSize = 0x1000
        };

        Assert.Success(fs.EnsureApplicationSaveData(out _, applicationId, in controlProperty, in userId));

        using var iterator = new UniqueRef<SaveDataIterator>();
        fs.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.User);

        var info = new SaveDataInfo[2];
        Assert.Success(iterator.Get.ReadSaveDataInfo(out long entriesRead, info));

        Assert.Equal(1, entriesRead);
        Assert.Equal(applicationId, info[0].ProgramId);
        Assert.Equal(Utility.ConvertAccountUidToFsUserId(userId), info[0].UserId);
        Assert.Equal(SaveDataType.Account, info[0].Type);
    }

    [Fact]
    public static void EnsureApplicationSaveData_CreatesDeviceSaveData()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        var applicationId = new Ncm.ApplicationId(11);
        var userId = new Uid(2, 3);

        var controlProperty = new ApplicationControlProperty
        {
            DeviceSaveDataSize = 0x1000,
            DeviceSaveDataJournalSize = 0x1000
        };

        Assert.Success(fs.EnsureApplicationSaveData(out _, applicationId, in controlProperty, in userId));

        using var iterator = new UniqueRef<SaveDataIterator>();
        fs.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.User);

        var info = new SaveDataInfo[2];
        Assert.Success(iterator.Get.ReadSaveDataInfo(out long entriesRead, info));

        Assert.Equal(1, entriesRead);
        Assert.Equal(applicationId, info[0].ProgramId);
        Assert.Equal(InvalidUserId, info[0].UserId);
        Assert.Equal(SaveDataType.Device, info[0].Type);
    }

    [Fact]
    public static void EnsureApplicationSaveData_CreatesBcatCacheStorage()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        var applicationId = new Ncm.ApplicationId(11);
        var userId = new Uid(2, 3);

        var controlProperty = new ApplicationControlProperty
        {
            BcatDeliveryCacheStorageSize = 0x1000
        };

        Assert.Success(fs.EnsureApplicationSaveData(out _, applicationId, in controlProperty, in userId));

        using var iterator = new UniqueRef<SaveDataIterator>();
        fs.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.User);

        var info = new SaveDataInfo[2];
        Assert.Success(iterator.Get.ReadSaveDataInfo(out long entriesRead, info));

        Assert.Equal(1, entriesRead);
        Assert.Equal(applicationId, info[0].ProgramId);
        Assert.Equal(InvalidUserId, info[0].UserId);
        Assert.Equal(SaveDataType.Bcat, info[0].Type);
    }

    [Fact]
    public static void EnsureApplicationSaveData_CreatesTemporaryStorage()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        var applicationId = new Ncm.ApplicationId(11);
        var userId = new Uid(2, 3);

        var controlProperty = new ApplicationControlProperty
        {
            TemporaryStorageSize = 0x1000
        };

        Assert.Success(fs.EnsureApplicationSaveData(out _, applicationId, in controlProperty, in userId));

        using var iterator = new UniqueRef<SaveDataIterator>();
        fs.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.Temporary);

        var info = new SaveDataInfo[2];
        Assert.Success(iterator.Get.ReadSaveDataInfo(out long entriesRead, info));

        Assert.Equal(1, entriesRead);
        Assert.Equal(applicationId, info[0].ProgramId);
        Assert.Equal(InvalidUserId, info[0].UserId);
        Assert.Equal(SaveDataType.Temporary, info[0].Type);
    }
    [Fact]
    public static void EnsureApplicationSaveData_NeedsExtension_IsExtended()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        var applicationId = new Ncm.ApplicationId(11);
        var userId = new Uid(2, 3);

        var controlProperty = new ApplicationControlProperty
        {
            UserAccountSaveDataSize = 0x1000,
            UserAccountSaveDataJournalSize = 0x1000
        };

        Assert.Success(fs.EnsureApplicationSaveData(out _, applicationId, in controlProperty, in userId));

        const int newAvailableSize = 1024 * 1024 * 2;
        const int newJournalSize = 1024 * 1024;

        controlProperty.UserAccountSaveDataSize = newAvailableSize;
        controlProperty.UserAccountSaveDataJournalSize = newJournalSize;

        Assert.Success(fs.EnsureApplicationSaveData(out _, applicationId, in controlProperty, in userId));

        using var iterator = new UniqueRef<SaveDataIterator>();
        fs.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.User);

        var info = new SaveDataInfo[2];
        Assert.Success(iterator.Get.ReadSaveDataInfo(out long entriesRead, info));

        Assert.Equal(1, entriesRead);
        Assert.Equal(applicationId, info[0].ProgramId);
        Assert.Equal(Utility.ConvertAccountUidToFsUserId(userId), info[0].UserId);
        Assert.Equal(SaveDataType.Account, info[0].Type);

        // ReSharper disable UnusedVariable
        Assert.Success(fs.GetSaveDataAvailableSize(out long availableSize, SaveDataSpaceId.User, info[0].SaveDataId));
        Assert.Success(fs.GetSaveDataJournalSize(out long journalSize, SaveDataSpaceId.User, info[0].SaveDataId));
        // ReSharper restore UnusedVariable

        // Todo: Remove once save data extension is implemented
        // Assert.Equal(newAvailableSize, availableSize);
        // Assert.Equal(newJournalSize, journalSize);
    }

    [Fact]
    public static void EnsureApplicationCacheStorage_SdCardAvailable_CreatesCacheStorageOnSd()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        var applicationId = new Ncm.ApplicationId(11);

        var controlProperty = new ApplicationControlProperty
        {
            CacheStorageSize = 0x1000,
            CacheStorageJournalSize = 0x1000
        };

        Assert.Success(fs.EnsureApplicationCacheStorage(out _, out CacheStorageTargetMedia target, applicationId,
            in controlProperty));

        Assert.Equal(CacheStorageTargetMedia.SdCard, target);

        using var iterator = new UniqueRef<SaveDataIterator>();
        fs.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.SdUser);

        var info = new SaveDataInfo[2];
        Assert.Success(iterator.Get.ReadSaveDataInfo(out long entriesRead, info));

        Assert.Equal(1, entriesRead);
        Assert.Equal(applicationId, info[0].ProgramId);
        Assert.Equal(SaveDataType.Cache, info[0].Type);
    }

    [Fact]
    public static void EnsureApplicationCacheStorage_SdCardNotAvailable_CreatesCacheStorageOnBis()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

        var applicationId = new Ncm.ApplicationId(11);

        var controlProperty = new ApplicationControlProperty
        {
            CacheStorageSize = 0x1000,
            CacheStorageJournalSize = 0x1000
        };

        Assert.Success(fs.EnsureApplicationCacheStorage(out _, out CacheStorageTargetMedia target, applicationId,
            in controlProperty));

        Assert.Equal(CacheStorageTargetMedia.Nand, target);

        using var iterator = new UniqueRef<SaveDataIterator>();
        fs.OpenSaveDataIterator(ref iterator.Ref(), SaveDataSpaceId.User);

        var info = new SaveDataInfo[2];
        Assert.Success(iterator.Get.ReadSaveDataInfo(out long entriesRead, info));

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

        var controlProperty = new ApplicationControlProperty
        {
            CacheStorageSize = 0x1000,
            CacheStorageJournalSize = 0x1000
        };

        fs.EnsureApplicationCacheStorage(out _, out CacheStorageTargetMedia targetFromCreation, applicationId,
            in controlProperty);

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