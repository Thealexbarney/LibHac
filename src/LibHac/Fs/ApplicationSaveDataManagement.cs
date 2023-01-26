using System;
using System.Runtime.CompilerServices;
using LibHac.Account;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.Ns;
using LibHac.Util;

namespace LibHac.Fs;

/// <summary>
/// Contains functions for ensuring that an application's save data exists and is the correct size.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0</remarks>
public static class ApplicationSaveDataManagement
{
    private const int LeftoverFreeSpaceRequiredForUserAndDeviceSaves = 0x4000;
    private const int SaveDataOverheadSize = 0x4000;
    private const int UserAndDeviceSaveDataOverheadSize = SaveDataOverheadSize + LeftoverFreeSpaceRequiredForUserAndDeviceSaves;
    private const int SaveDataBlockSize = 0x4000;
    private const int SaveDataExtensionSizeAlignment = 0x100000; // 1 MiB

    private static long RoundUpOccupationSize(long size)
    {
        return Alignment.AlignUp(size, SaveDataBlockSize);
    }

    private static long CalculateSaveDataExtensionContextFileSize(long saveDataSize, long saveDataJournalSize)
    {
        return RoundUpOccupationSize((saveDataSize + saveDataJournalSize) / 0x400 + 0x100000);
    }

    private static Result ExtendSaveDataIfNeeded(FileSystemClient fs, ref long outRequiredSize, SaveDataSpaceId spaceId,
        ulong saveDataId, long saveDataSize, long saveDataJournalSize)
    {
        return Result.Success;

        // Todo: Remove early return once extending save data is implemented
#pragma warning disable CS0162
        // ReSharper disable HeuristicUnreachableCode

        // Get the current save data size
        Result res = fs.Impl.GetSaveDataAvailableSize(out long availableSize, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.GetSaveDataJournalSize(out long journalSize, spaceId, saveDataId);
        if (res.IsFailure()) return res.Miss();

        // Check if the save data needs to be extended
        if (availableSize < saveDataSize || journalSize < saveDataJournalSize)
        {
            // Make sure the new sizes are valid
            if (availableSize < saveDataSize && !Alignment.IsAligned(saveDataSize, SaveDataExtensionSizeAlignment))
                return ResultFs.ExtensionSizeInvalid.Log();

            if (journalSize < saveDataJournalSize && !Alignment.IsAligned(saveDataJournalSize, SaveDataExtensionSizeAlignment))
                return ResultFs.ExtensionSizeInvalid.Log();

            long newSaveDataSize = Math.Max(saveDataSize, availableSize);
            long newSaveDataJournalSize = Math.Max(saveDataJournalSize, journalSize);

            res = fs.Impl.ExtendSaveData(spaceId, saveDataId, newSaveDataSize, newSaveDataJournalSize);

            if (res.IsFailure())
            {
                if (ResultFs.UsableSpaceNotEnough.Includes(res))
                {
                    // Calculate how much space we need to extend the save data
                    res = fs.QuerySaveDataTotalSize(out long currentSaveDataTotalSize, availableSize, journalSize);
                    if (res.IsFailure()) return res.Miss();

                    res = fs.QuerySaveDataTotalSize(out long newSaveDataTotalSize, newSaveDataSize, newSaveDataJournalSize);
                    if (res.IsFailure()) return res.Miss();

                    long newSaveDataSizeDifference = RoundUpOccupationSize(newSaveDataTotalSize) -
                                                     RoundUpOccupationSize(currentSaveDataTotalSize);

                    outRequiredSize += newSaveDataSizeDifference +
                                       CalculateSaveDataExtensionContextFileSize(newSaveDataSize,
                                           newSaveDataJournalSize) + UserAndDeviceSaveDataOverheadSize;

                    return ResultFs.UsableSpaceNotEnough.Log();
                }

                return res.Miss();
            }
        }

        return Result.Success;
        // ReSharper restore HeuristicUnreachableCode
#pragma warning restore CS0162
    }

    private static Result CreateSaveData(FileSystemClient fs, ref long inOutRequiredSize, Func<Result> createFunc,
        long saveDataSize, long saveDataJournalSize, long saveDataBaseSize)
    {
        Result res = createFunc();

        if (res.IsSuccess())
            return Result.Success;

        if (res.IsFailure())
        {
            if (ResultFs.UsableSpaceNotEnough.Includes(res))
            {
                res = fs.QuerySaveDataTotalSize(out long saveDataTotalSize, saveDataSize, saveDataJournalSize);
                if (res.IsFailure()) return res.Miss();

                inOutRequiredSize += RoundUpOccupationSize(saveDataTotalSize) + saveDataBaseSize;
            }
            else if (ResultFs.PathAlreadyExists.Includes(res))
            {
                return Result.Success;
            }

            return res.Miss();
        }

        return Result.Success;
    }

    private static Result EnsureAndExtendSaveData(FileSystemClient fs, ref long inOutRequiredSize,
        in SaveDataFilter filter, Func<Result> createFunc, long saveDataSize, long saveDataJournalSize,
        long saveDataBaseSize)
    {
        Result res = fs.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);

        if (res.IsFailure())
        {
            if (ResultFs.TargetNotFound.Includes(res))
            {
                res = CreateSaveData(fs, ref inOutRequiredSize, createFunc, saveDataSize, saveDataJournalSize,
                    saveDataBaseSize);
                if (res.IsFailure()) return res.Miss();

                return Result.Success;
            }

            return res.Miss();
        }

        long requiredSize = 0;
        res = ExtendSaveDataIfNeeded(fs, ref requiredSize, SaveDataSpaceId.User, info.SaveDataId, saveDataSize,
            saveDataJournalSize);

        if (res.IsFailure())
        {
            if (ResultFs.UsableSpaceNotEnough.Includes(res))
            {
                inOutRequiredSize += requiredSize;
            }

            return res.Miss();
        }

        return Result.Success;
    }

    private static Result CheckSaveDataType(SaveDataType type, in Uid user)
    {
        switch (type)
        {
            case SaveDataType.Account:
            {
                if (user == Uid.InvalidUid)
                    return ResultFs.InvalidArgument.Log();

                return Result.Success;
            }
            case SaveDataType.Device:
            {
                if (user == Uid.InvalidUid)
                    return ResultFs.InvalidArgument.Log();

                return Result.Success;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    private static Result CheckExtensionSizeUnderMax(SaveDataType type, in ApplicationControlProperty controlProperty,
        long saveDataSize, long saveDataJournalSize)
    {
        switch (type)
        {
            case SaveDataType.Account:
            {
                if (saveDataSize > controlProperty.UserAccountSaveDataSizeMax ||
                    saveDataJournalSize > controlProperty.UserAccountSaveDataJournalSizeMax)
                {
                    return ResultFs.ExtensionSizeTooLarge.Log();
                }

                return Result.Success;
            }
            case SaveDataType.Device:
            {
                if (saveDataSize > controlProperty.DeviceSaveDataSizeMax ||
                    saveDataJournalSize > controlProperty.DeviceSaveDataJournalSizeMax)
                {
                    return ResultFs.ExtensionSizeTooLarge.Log();
                }

                return Result.Success;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    private static Result EnsureApplicationBcatDeliveryCacheStorageImpl(FileSystemClient fs, ref long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty)
    {
        const long bcatDeliveryCacheJournalSize = 0x200000;

        long requiredSize = 0;
        long bcatDeliveryCacheStorageSize = controlProperty.BcatDeliveryCacheStorageSize;

        if (bcatDeliveryCacheStorageSize > 0)
        {
            Result res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Bcat,
                userId: default, saveDataId: default, index: default);
            if (res.IsFailure()) return res.Miss();

            Result CreateBcatStorageFunc() => fs.CreateBcatSaveData(applicationId, bcatDeliveryCacheStorageSize);

            res = EnsureAndExtendSaveData(fs, ref requiredSize, in filter, CreateBcatStorageFunc,
                bcatDeliveryCacheStorageSize, bcatDeliveryCacheJournalSize, SaveDataOverheadSize);
            if (res.IsFailure()) return res.Miss();
        }

        if (requiredSize == 0)
        {
            outRequiredSize = 0;
            return Result.Success;
        }

        outRequiredSize = requiredSize;
        return ResultFs.UsableSpaceNotEnough.Log();
    }

    private static Result GetCacheStorageTargetMediaImpl(FileSystemClient fs, out CacheStorageTargetMedia targetMedia,
        Ncm.ApplicationId applicationId)
    {
        UnsafeHelpers.SkipParamInit(out targetMedia);
        Result res;

        if (fs.IsSdCardAccessible())
        {
            res = DoesCacheStorageExist(out bool existsOnSd, SaveDataSpaceId.SdUser, fs, applicationId);
            if (res.IsFailure()) return res.Miss();

            if (existsOnSd)
            {
                targetMedia = CacheStorageTargetMedia.SdCard;
                return Result.Success;
            }
        }

        res = DoesCacheStorageExist(out bool existsOnNand, SaveDataSpaceId.User, fs, applicationId);
        if (res.IsFailure()) return res.Miss();

        targetMedia = existsOnNand ? CacheStorageTargetMedia.Nand : CacheStorageTargetMedia.None;
        return Result.Success;

        static Result DoesCacheStorageExist(out bool exists, SaveDataSpaceId spaceId, FileSystemClient fs,
            Ncm.ApplicationId applicationId)
        {
            UnsafeHelpers.SkipParamInit(out exists);

            bool doesStorageExist = true;

            Result res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Cache,
                userId: default, saveDataId: default, index: default);
            if (res.IsFailure()) return res.Miss();

            res = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo _, spaceId, in filter);

            if (res.IsFailure())
            {
                if (!ResultFs.TargetNotFound.Includes(res))
                    return res.Miss();

                doesStorageExist = false;
            }

            exists = doesStorageExist;
            return Result.Success;
        }
    }

    private static Result TryCreateCacheStorage(this FileSystemClient fs, ref long outRequiredSize,
        SaveDataSpaceId spaceId, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
        long cacheStorageSize, long cacheStorageJournalSize, bool allowExisting)
    {
        long requiredSize = 0;

        Result res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Cache,
            userId: default, saveDataId: default, index);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        // Check if the cache storage already exists or not.
        bool doesStorageExist = true;
        res = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, spaceId, in filter);

        if (res.IsFailure())
        {
            if (ResultFs.TargetNotFound.Includes(res))
            {
                doesStorageExist = false;
            }
            else
            {
                fs.Impl.AbortIfNeeded(res);
                return res.Miss();
            }
        }

        if (doesStorageExist)
        {
            // The cache storage already exists. Ensure it's large enough.
            if (!allowExisting)
            {
                res = ResultFs.AlreadyExists.Value;
                fs.Impl.AbortIfNeeded(res);
                return res.Miss();
            }

            res = ExtendSaveDataIfNeeded(fs, ref requiredSize, spaceId, info.SaveDataId, cacheStorageSize,
                cacheStorageJournalSize);

            if (res.IsFailure())
            {
                if (ResultFs.UsableSpaceNotEnough.Includes(res))
                {
                    // Don't return this error. If there's not enough space we return Success along with
                    // the amount of space required to create the cache storage.
                }
                else if (ResultFs.SaveDataExtending.Includes(res))
                {
                    res = ResultFs.SaveDataCorrupted.LogConverted(res);
                    fs.Impl.AbortIfNeeded(res);
                    return res.Miss();
                }
                else
                {
                    res.Miss();
                    fs.Impl.AbortIfNeeded(res);
                    return res;
                }
            }
        }
        else
        {
            // The cache storage doesn't exist. Try to create it.
            Result CreateCacheFunc() => fs.CreateCacheStorage(applicationId, spaceId, saveDataOwnerId, index,
                cacheStorageSize, cacheStorageJournalSize, SaveDataFlags.None);

            res = CreateSaveData(fs, ref requiredSize, CreateCacheFunc, cacheStorageSize, cacheStorageJournalSize,
                SaveDataOverheadSize);

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();
        }

        outRequiredSize = requiredSize;
        return Result.Success;
    }

    private static Result EnsureApplicationCacheStorageImpl(this FileSystemClient fs, ref long outRequiredSize,
        out CacheStorageTargetMedia outTargetMedia, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
        long cacheStorageSize, long cacheStorageJournalSize, bool allowExisting)
    {
        outTargetMedia = CacheStorageTargetMedia.SdCard;
        long requiredSize = 0;

        // Check if the cache storage already exists
        Result res = GetCacheStorageTargetMediaImpl(fs, out CacheStorageTargetMedia targetMedia, applicationId);
        if (res.IsFailure()) return res.Miss();

        if (targetMedia == CacheStorageTargetMedia.SdCard)
        {
            // If it exists on the SD card, ensure it's large enough.
            outTargetMedia = CacheStorageTargetMedia.SdCard;

            res = TryCreateCacheStorage(fs, ref requiredSize, SaveDataSpaceId.SdUser, applicationId, saveDataOwnerId,
                index, cacheStorageSize, cacheStorageJournalSize, allowExisting);
            if (res.IsFailure()) return res.Miss();
        }
        else if (targetMedia == CacheStorageTargetMedia.Nand)
        {
            // If it exists on the BIS, ensure it's large enough.
            outTargetMedia = CacheStorageTargetMedia.Nand;

            res = TryCreateCacheStorage(fs, ref requiredSize, SaveDataSpaceId.User, applicationId, saveDataOwnerId,
                index, cacheStorageSize, cacheStorageJournalSize, allowExisting);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            // The cache storage doesn't exist. Try to create it on the SD card first.
            bool isSdCardAccessible = fs.IsSdCardAccessible();
            if (isSdCardAccessible)
            {
                outTargetMedia = CacheStorageTargetMedia.SdCard;

                Result CreateStorageOnSdCard() => fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdUser,
                    saveDataOwnerId, index, cacheStorageSize, cacheStorageJournalSize, SaveDataFlags.None);

                res = CreateSaveData(fs, ref requiredSize, CreateStorageOnSdCard, cacheStorageSize, cacheStorageJournalSize,
                    SaveDataOverheadSize);
                if (res.IsFailure()) return res.Miss();

                // Don't use the SD card if it doesn't have enough space.
                if (requiredSize != 0)
                    isSdCardAccessible = false;
            }

            // If the cache storage can't be created on the SD card, try creating it on the User BIS partition.
            if (!isSdCardAccessible)
            {
                requiredSize = 0;
                outTargetMedia = CacheStorageTargetMedia.Nand;

                Result CreateStorageOnNand() => fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, saveDataOwnerId,
                    index, cacheStorageSize, cacheStorageSize, SaveDataFlags.None);

                res = CreateSaveData(fs, ref requiredSize, CreateStorageOnNand, cacheStorageSize, cacheStorageJournalSize,
                    SaveDataOverheadSize);
                if (res.IsFailure()) return res.Miss();

                if (requiredSize != 0)
                    outTargetMedia = CacheStorageTargetMedia.None;
            }
        }

        outRequiredSize = requiredSize;

        if (requiredSize != 0)
            return ResultFs.UsableSpaceNotEnough.Log();

        return Result.Success;
    }

    public static Result EnsureApplicationDeviceSaveData(this FileSystemClientImpl fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, long saveDataSize, long saveDataJournalSize)
    {
        UnsafeHelpers.SkipParamInit(out outRequiredSize);

        long requiredSize = 0;

        Result res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Device,
            userId: default, saveDataId: default, index: default);
        if (res.IsFailure()) return res.Miss();

        const SaveDataFlags flags = SaveDataFlags.None;

        Result CreateSave() =>
            fs.CreateDeviceSaveData(applicationId, applicationId.Value, saveDataSize, saveDataJournalSize, flags);

        res = EnsureAndExtendSaveData(fs.Fs, ref requiredSize, in filter, CreateSave, saveDataSize, saveDataJournalSize,
            SaveDataOverheadSize);
        if (res.IsFailure()) return res.Miss();

        outRequiredSize = requiredSize;
        return Result.Success;
    }

    public static Result GetCacheStorageTargetMedia(this FileSystemClient fs, out CacheStorageTargetMedia targetMedia,
        Ncm.ApplicationId applicationId)
    {
        Result res = GetCacheStorageTargetMediaImpl(fs, out targetMedia, applicationId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    // Removed in 15.x
    public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        out CacheStorageTargetMedia targetMedia, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
        long cacheStorageSize, long cacheStorageJournalSize, bool allowExisting)
    {
        outRequiredSize = 0;

        Result res = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out targetMedia, applicationId,
            saveDataOwnerId, index, cacheStorageSize, cacheStorageJournalSize, allowExisting);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty)
    {
        outRequiredSize = 0;

        Result res = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out _, applicationId,
            controlProperty.SaveDataOwnerId, index: 0, controlProperty.CacheStorageSize,
            controlProperty.CacheStorageJournalSize, allowExisting: true);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        out CacheStorageTargetMedia targetMedia, Ncm.ApplicationId applicationId,
        in ApplicationControlProperty controlProperty)
    {
        targetMedia = CacheStorageTargetMedia.None;
        outRequiredSize = 0;

        if (controlProperty.CacheStorageSize > 0)
        {
            Result res = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out targetMedia, applicationId,
                controlProperty.SaveDataOwnerId, index: 0, controlProperty.CacheStorageSize,
                controlProperty.CacheStorageJournalSize, true);

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public static Result CreateApplicationCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        out CacheStorageTargetMedia targetMedia, Ncm.ApplicationId applicationId,
        in ApplicationControlProperty controlProperty, ushort index, long cacheStorageSize,
        long cacheStorageJournalSize)
    {
        UnsafeHelpers.SkipParamInit(out outRequiredSize, out targetMedia);

        Result res;

        if (index > controlProperty.CacheStorageIndexMax)
        {
            res = ResultFs.CacheStorageIndexTooLarge.Value;
            fs.Impl.AbortIfNeeded(res);
            return res.Miss();
        }

        if (cacheStorageSize + cacheStorageJournalSize > controlProperty.CacheStorageDataAndJournalSizeMax)
        {
            res = ResultFs.CacheStorageSizeTooLarge.Value;
            fs.Impl.AbortIfNeeded(res);
            return res.Miss();
        }

        res = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out targetMedia, applicationId,
            controlProperty.SaveDataOwnerId, index, cacheStorageSize, cacheStorageJournalSize, allowExisting: false);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result CleanUpTemporaryStorage(this FileSystemClient fs)
    {
        Result res = fs.Impl.CleanUpTemporaryStorageImpl();

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result EnsureApplicationBcatDeliveryCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty)
    {
        outRequiredSize = 0;

        Result res = EnsureApplicationBcatDeliveryCacheStorageImpl(fs, ref outRequiredSize, applicationId,
            in controlProperty);

        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result EnsureApplicationSaveData(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty, in Uid user)
    {
        UnsafeHelpers.SkipParamInit(out outRequiredSize);

        using var prohibiter = new UniqueRef<SaveDataTransferProhibiterForCloudBackUp>();
        Result res = fs.Impl.OpenSaveDataTransferProhibiterForCloudBackUp(ref prohibiter.Ref, applicationId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        long requiredSize = 0;

        // Create local variables for use in closures
        Uid uid = user;
        ulong saveDataOwnerId = controlProperty.SaveDataOwnerId;
        long accountSaveDataSize = controlProperty.UserAccountSaveDataSize;
        long accountSaveJournalSize = controlProperty.UserAccountSaveDataJournalSize;
        long deviceSaveDataSize = controlProperty.DeviceSaveDataSize;
        long deviceSaveJournalSize = controlProperty.DeviceSaveDataJournalSize;

        // Ensure the user account save exists
        if (user != Uid.InvalidUid && controlProperty.UserAccountSaveDataSize > 0)
        {
            Result CreateAccountSaveFunc()
            {
                UserId fsUserId = Utility.ConvertAccountUidToFsUserId(uid);
                return fs.Impl.CreateSaveData(applicationId, fsUserId, saveDataOwnerId, accountSaveDataSize,
                    accountSaveJournalSize, SaveDataFlags.None);
            }

            UserId userId = Unsafe.As<Uid, UserId>(ref Unsafe.AsRef(in user));
            res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Account, userId,
                saveDataId: default, index: default);

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            long baseSize = RoundUpOccupationSize(new SaveDataMetaPolicy(SaveDataType.Account).GetSaveDataMetaSize()) +
                            UserAndDeviceSaveDataOverheadSize;

            res = EnsureAndExtendSaveData(fs, ref requiredSize, in filter, CreateAccountSaveFunc, accountSaveDataSize,
                accountSaveJournalSize, baseSize);

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();
        }

        // Ensure the device save exists
        if (controlProperty.DeviceSaveDataSize > 0)
        {
            Result CreateDeviceSaveFunc() => fs.Impl.CreateDeviceSaveData(applicationId, saveDataOwnerId,
                deviceSaveDataSize, deviceSaveJournalSize, SaveDataFlags.None);

            res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Device,
                userId: default, saveDataId: default, index: default);

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            long baseSize = RoundUpOccupationSize(new SaveDataMetaPolicy(SaveDataType.Device).GetSaveDataMetaSize()) +
                            UserAndDeviceSaveDataOverheadSize;

            long requiredSizeForDeviceSaveData = 0;
            res = EnsureAndExtendSaveData(fs, ref requiredSizeForDeviceSaveData, in filter, CreateDeviceSaveFunc,
                deviceSaveDataSize, deviceSaveJournalSize, baseSize);

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            if (requiredSizeForDeviceSaveData == 0)
            {
                res = fs.Impl.CreateDeviceSaveData(applicationId, saveDataOwnerId, deviceSaveDataSize,
                    deviceSaveJournalSize, SaveDataFlags.None);

                if (res.IsFailure())
                {
                    if (ResultFs.PathAlreadyExists.Includes(res))
                    {
                        // Nothing to do if the save already exists.
                        res.Catch().Handle();
                    }
                    else if (ResultFs.UsableSpaceNotEnough.Includes(res))
                    {
                        requiredSizeForDeviceSaveData +=
                            RoundUpOccupationSize(new SaveDataMetaPolicy(SaveDataType.Device).GetSaveDataMetaSize()) +
                            SaveDataOverheadSize;
                    }
                    else
                    {
                        fs.Impl.AbortIfNeeded(res);
                        return res.Miss();
                    }
                }
            }

            requiredSize += requiredSizeForDeviceSaveData;
        }

        long requiredSizeForBcat = 0;
        res = EnsureApplicationBcatDeliveryCacheStorageImpl(fs, ref requiredSizeForBcat, applicationId,
            in controlProperty);

        if (res.IsFailure())
        {
            if (ResultFs.UsableSpaceNotEnough.Includes(res))
            {
                requiredSize += requiredSizeForBcat;
            }
            else
            {
                fs.Impl.AbortIfNeeded(res);
                return res.Miss();
            }
        }

        if (controlProperty.TemporaryStorageSize > 0)
        {
            static Result CalculateRequiredSizeForTemporaryStorage(FileSystemClient fs, out long requiredSize,
                in ApplicationControlProperty controlProperty)
            {
                UnsafeHelpers.SkipParamInit(out requiredSize);

                Result res = fs.Impl.QuerySaveDataTotalSize(out long saveDataTotalSize,
                    controlProperty.TemporaryStorageSize, saveDataJournalSize: 0);
                if (res.IsFailure()) return res.Miss();

                requiredSize = RoundUpOccupationSize(saveDataTotalSize) + SaveDataOverheadSize;
                return Result.Success;
            }

            if (requiredSize == 0)
            {
                res = fs.Impl.CreateTemporaryStorage(applicationId, saveDataOwnerId,
                    controlProperty.TemporaryStorageSize, SaveDataFlags.None);

                if (res.IsFailure())
                {
                    if (ResultFs.UsableSpaceNotEnough.Includes(res))
                    {
                        res = CalculateRequiredSizeForTemporaryStorage(fs, out long temporaryStorageSize,
                            in controlProperty);

                        fs.Impl.AbortIfNeeded(res);
                        if (res.IsFailure()) return res.Miss();

                        requiredSize += temporaryStorageSize;
                    }
                    else if (ResultFs.PathAlreadyExists.Includes(res))
                    {
                        // Nothing to do if the save already exists.
                        res.Catch().Handle();
                    }
                    else
                    {
                        fs.Impl.AbortIfNeeded(res);
                        return res.Miss();
                    }
                }
            }
            else
            {
                // If there was already insufficient space to create the previous saves, don't try to create a
                // temporary save. Just calculate the space required, if any, to create it.
                res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Temporary,
                    userId: default, saveDataId: default, index: default);

                fs.Impl.AbortIfNeeded(res);
                if (res.IsFailure()) return res.Miss();

                // Check if the temporary save exists
                res = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo _, SaveDataSpaceId.Temporary, in filter);

                if (res.IsFailure())
                {
                    if (ResultFs.TargetNotFound.Includes(res))
                    {
                        // If it doesn't exist, calculate the size required to create it
                        res = CalculateRequiredSizeForTemporaryStorage(fs, out long temporaryStorageSize,
                            in controlProperty);

                        fs.Impl.AbortIfNeeded(res);
                        if (res.IsFailure()) return res.Miss();

                        requiredSize += temporaryStorageSize;
                    }
                    else
                    {
                        fs.Impl.AbortIfNeeded(res);
                        return res.Miss();
                    }
                }
            }
        }

        if (requiredSize == 0)
        {
            outRequiredSize = 0;
            return Result.Success;
        }

        outRequiredSize = requiredSize;

        res = ResultFs.UsableSpaceNotEnough.Log();
        fs.Impl.AbortIfNeeded(res);
        return res;
    }

    public static Result ExtendApplicationSaveData(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty, SaveDataType type, in Uid user,
        long saveDataSize, long saveDataJournalSize)
    {
        UnsafeHelpers.SkipParamInit(out outRequiredSize);

        Result res = CheckSaveDataType(type, in user);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        UserId userId = Unsafe.As<Uid, UserId>(ref Unsafe.AsRef(in user));
        res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, type, userId, saveDataId: default,
            index: default);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = CheckExtensionSizeUnderMax(type, in controlProperty, saveDataSize, saveDataJournalSize);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        long requiredSize = 0;
        res = ExtendSaveDataIfNeeded(fs, ref requiredSize, SaveDataSpaceId.User, info.SaveDataId, saveDataSize,
            saveDataJournalSize);

        if (res.IsFailure())
        {
            if (ResultFs.UsableSpaceNotEnough.Includes(res))
            {
                outRequiredSize = requiredSize;

                fs.Impl.AbortIfNeeded(res);
                return res.Rethrow();
            }

            fs.Impl.AbortIfNeeded(res);
            return res.Miss();
        }

        return Result.Success;
    }

    public static Result GetApplicationSaveDataSize(this FileSystemClient fs, out long outSaveDataSize,
        out long outSaveDataJournalSize, Ncm.ApplicationId applicationId, SaveDataType type, in Uid user)
    {
        UnsafeHelpers.SkipParamInit(out outSaveDataSize, out outSaveDataJournalSize);

        Result res = CheckSaveDataType(type, in user);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        UserId userId = Unsafe.As<Uid, UserId>(ref Unsafe.AsRef(in user));
        res = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, type, userId, saveDataId: default,
            index: default);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.GetSaveDataAvailableSize(out long saveDataSize, info.SaveDataId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        res = fs.Impl.GetSaveDataJournalSize(out long saveDataJournalSize, info.SaveDataId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        outSaveDataSize = saveDataSize;
        outSaveDataJournalSize = saveDataJournalSize;

        return Result.Success;
    }
}