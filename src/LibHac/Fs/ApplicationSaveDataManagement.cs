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
/// <remarks>Based on nnSdk 13.4.0</remarks>
public static class ApplicationSaveDataManagement
{
    private const int SaveDataBlockSize = 0x4000;
    private const int SaveDataExtensionSizeAlignment = 0x100000; // 1 MiB

    private static long RoundUpOccupationSize(long size)
    {
        return Alignment.AlignUpPow2(size, SaveDataBlockSize);
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
        Result rc = fs.Impl.GetSaveDataAvailableSize(out long availableSize, spaceId, saveDataId);
        if (rc.IsFailure()) return rc.Miss();

        rc = fs.Impl.GetSaveDataJournalSize(out long journalSize, spaceId, saveDataId);
        if (rc.IsFailure()) return rc.Miss();

        // Check if the save data needs to be extended
        if (availableSize < saveDataSize || journalSize < saveDataJournalSize)
        {
            // Make sure the new sizes are valid
            if (availableSize < saveDataSize && !Alignment.IsAlignedPow2(saveDataSize, SaveDataExtensionSizeAlignment))
                return ResultFs.ExtensionSizeInvalid.Log();

            if (journalSize < saveDataJournalSize && !Alignment.IsAlignedPow2(saveDataJournalSize, SaveDataExtensionSizeAlignment))
                return ResultFs.ExtensionSizeInvalid.Log();

            long newSaveDataSize = Math.Max(saveDataSize, availableSize);
            long newSaveDataJournalSize = Math.Max(saveDataJournalSize, journalSize);

            rc = fs.Impl.ExtendSaveData(spaceId, saveDataId, newSaveDataSize, newSaveDataJournalSize);

            if (rc.IsFailure())
            {
                if (ResultFs.UsableSpaceNotEnough.Includes(rc))
                {
                    // Calculate how much space we need to extend the save data
                    rc = fs.QuerySaveDataTotalSize(out long currentSaveDataTotalSize, availableSize, journalSize);
                    if (rc.IsFailure()) return rc.Miss();

                    rc = fs.QuerySaveDataTotalSize(out long newSaveDataTotalSize, newSaveDataSize, newSaveDataJournalSize);
                    if (rc.IsFailure()) return rc.Miss();

                    long newSaveDataSizeDifference = RoundUpOccupationSize(newSaveDataTotalSize) -
                                                     RoundUpOccupationSize(currentSaveDataTotalSize);

                    outRequiredSize += newSaveDataSizeDifference +
                                       CalculateSaveDataExtensionContextFileSize(newSaveDataSize,
                                           newSaveDataJournalSize) + 0x8000;

                    return ResultFs.UsableSpaceNotEnough.Log();
                }

                return rc.Miss();
            }
        }

        return Result.Success;
        // ReSharper restore HeuristicUnreachableCode
#pragma warning restore CS0162
    }

    private static Result CreateSaveData(FileSystemClient fs, ref long inOutRequiredSize, Func<Result> createFunc,
        long saveDataSize, long saveDataJournalSize, long saveDataBaseSize)
    {
        Result rc = createFunc();

        if (rc.IsSuccess())
            return Result.Success;

        if (ResultFs.UsableSpaceNotEnough.Includes(rc))
        {
            rc = fs.QuerySaveDataTotalSize(out long saveDataTotalSize, saveDataSize, saveDataJournalSize);
            if (rc.IsFailure()) return rc;

            inOutRequiredSize += RoundUpOccupationSize(saveDataTotalSize) + saveDataBaseSize;
        }
        else if (!ResultFs.PathAlreadyExists.Includes(rc))
        {
            return rc.Miss();
        }

        return Result.Success;
    }

    private static Result EnsureAndExtendSaveData(FileSystemClient fs, ref long inOutRequiredSize,
        in SaveDataFilter filter, Func<Result> createFunc, long saveDataSize, long saveDataJournalSize,
        long saveDataBaseSize)
    {
        Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);

        if (rc.IsFailure())
        {
            if (ResultFs.TargetNotFound.Includes(rc))
            {
                rc = CreateSaveData(fs, ref inOutRequiredSize, createFunc, saveDataSize, saveDataJournalSize,
                    saveDataBaseSize);
                if (rc.IsFailure()) return rc.Miss();

                return Result.Success;
            }

            return rc.Miss();
        }

        long requiredSize = 0;
        rc = ExtendSaveDataIfNeeded(fs, ref requiredSize, SaveDataSpaceId.User, info.SaveDataId, saveDataSize,
            saveDataJournalSize);

        if (rc.IsFailure())
        {
            if (!ResultFs.UsableSpaceNotEnough.Includes(rc))
                return rc.Miss();

            inOutRequiredSize += requiredSize;
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
            Result rc = SaveDataFilter.Make(out SaveDataFilter filter, programId: default, SaveDataType.Bcat,
                userId: default, saveDataId: default, index: default);
            if (rc.IsFailure()) return rc.Miss();

            Result CreateBcatStorageFunc() => fs.CreateBcatSaveData(applicationId, bcatDeliveryCacheStorageSize);

            rc = EnsureAndExtendSaveData(fs, ref requiredSize, in filter, CreateBcatStorageFunc,
                bcatDeliveryCacheStorageSize, bcatDeliveryCacheJournalSize, 0x4000);
            if (rc.IsFailure()) return rc.Miss();
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
        Result rc;

        if (fs.IsSdCardAccessible())
        {
            rc = DoesCacheStorageExist(out bool existsOnSd, SaveDataSpaceId.SdUser, fs, applicationId);
            if (rc.IsFailure()) return rc.Miss();

            if (existsOnSd)
            {
                targetMedia = CacheStorageTargetMedia.SdCard;
                return Result.Success;
            }
        }

        rc = DoesCacheStorageExist(out bool existsOnNand, SaveDataSpaceId.User, fs, applicationId);
        if (rc.IsFailure()) return rc.Miss();

        targetMedia = existsOnNand ? CacheStorageTargetMedia.Nand : CacheStorageTargetMedia.None;
        return Result.Success;

        static Result DoesCacheStorageExist(out bool exists, SaveDataSpaceId spaceId, FileSystemClient fs,
            Ncm.ApplicationId applicationId)
        {
            UnsafeHelpers.SkipParamInit(out exists);

            bool doesStorageExist = true;

            Result rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Cache,
                userId: default, saveDataId: default, index: default);
            if (rc.IsFailure()) return rc.Miss();

            rc = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo _, spaceId, in filter);

            if (rc.IsFailure())
            {
                if (!ResultFs.TargetNotFound.Includes(rc))
                    return rc.Miss();

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

        Result rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Cache,
            userId: default, saveDataId: default, index);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        // Check if the cache storage already exists or not.
        bool doesStorageExist = true;
        rc = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, spaceId, in filter);

        if (rc.IsFailure())
        {
            if (ResultFs.TargetNotFound.Includes(rc))
            {
                doesStorageExist = false;
            }
            else
            {
                fs.Impl.AbortIfNeeded(rc);
                return rc.Miss();
            }
        }

        if (doesStorageExist)
        {
            // The cache storage already exists. Ensure it's large enough.
            if (!allowExisting)
            {
                rc = ResultFs.AlreadyExists.Value;
                fs.Impl.AbortIfNeeded(rc);
                return rc.Miss();
            }

            rc = ExtendSaveDataIfNeeded(fs, ref requiredSize, spaceId, info.SaveDataId, cacheStorageSize,
                cacheStorageJournalSize);

            if (rc.IsFailure())
            {
                if (ResultFs.UsableSpaceNotEnough.Includes(rc))
                {
                    // Don't return this error. If there's not enough space we return Success along with
                    // The amount of space required to create the cache storage.
                }
                else if (ResultFs.SaveDataExtending.Includes(rc))
                {
                    rc = ResultFs.SaveDataCorrupted.LogConverted(rc);
                    fs.Impl.AbortIfNeeded(rc);
                    return rc.Miss();
                }
                else
                {
                    rc.Miss();
                    fs.Impl.AbortIfNeeded(rc);
                    return rc;
                }
            }
        }
        else
        {
            // The cache storage doesn't exist. Try to create it.
            Result CreateCacheFunc() => fs.CreateCacheStorage(applicationId, spaceId, saveDataOwnerId, index,
                cacheStorageSize, cacheStorageJournalSize, SaveDataFlags.None);

            rc = CreateSaveData(fs, ref requiredSize, CreateCacheFunc, cacheStorageSize, cacheStorageJournalSize,
                0x4000);

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();
        }

        outRequiredSize = requiredSize;
        return Result.Success;
    }

    private static Result EnsureApplicationCacheStorageImpl(this FileSystemClient fs, ref long outRequiredSize,
        out CacheStorageTargetMedia targetMedia, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
        long cacheStorageSize, long cacheStorageJournalSize, bool allowExisting)
    {
        targetMedia = CacheStorageTargetMedia.SdCard;
        long requiredSize = 0;

        // Check if the cache storage already exists
        Result rc = GetCacheStorageTargetMediaImpl(fs, out CacheStorageTargetMedia media, applicationId);
        if (rc.IsFailure()) return rc.Miss();

        if (media == CacheStorageTargetMedia.SdCard)
        {
            // If it exists on the SD card, ensure it's large enough.
            targetMedia = CacheStorageTargetMedia.SdCard;

            rc = TryCreateCacheStorage(fs, ref requiredSize, SaveDataSpaceId.SdUser, applicationId, saveDataOwnerId,
                index, cacheStorageSize, cacheStorageJournalSize, allowExisting);
            if (rc.IsFailure()) return rc.Miss();
        }
        else if (media == CacheStorageTargetMedia.Nand)
        {
            // If it exists on the BIS, ensure it's large enough.
            targetMedia = CacheStorageTargetMedia.Nand;

            rc = TryCreateCacheStorage(fs, ref requiredSize, SaveDataSpaceId.User, applicationId, saveDataOwnerId,
                index, cacheStorageSize, cacheStorageJournalSize, allowExisting);
            if (rc.IsFailure()) return rc.Miss();
        }
        else
        {
            // The cache storage doesn't exist. Try to create it on the SD card first.
            bool isSdCardAccessible = fs.IsSdCardAccessible();
            if (isSdCardAccessible)
            {
                targetMedia = CacheStorageTargetMedia.SdCard;

                Result CreateStorageOnSdCard() => fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdUser,
                    saveDataOwnerId, index, cacheStorageSize, cacheStorageJournalSize, SaveDataFlags.None);

                rc = CreateSaveData(fs, ref requiredSize, CreateStorageOnSdCard, cacheStorageSize, cacheStorageJournalSize,
                    0x4000);
                if (rc.IsFailure()) return rc.Miss();

                // Don't use the SD card if it doesn't have enough space.
                if (requiredSize != 0)
                    isSdCardAccessible = false;
            }

            // If the cache storage can't be created on the SD card, try creating it on the User BIS partition.
            if (!isSdCardAccessible)
            {
                requiredSize = 0;
                targetMedia = CacheStorageTargetMedia.Nand;

                Result CreateStorageOnNand() => fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, saveDataOwnerId,
                    index, cacheStorageSize, cacheStorageSize, SaveDataFlags.None);

                rc = CreateSaveData(fs, ref requiredSize, CreateStorageOnNand, cacheStorageSize, cacheStorageJournalSize,
                    0x4000);
                if (rc.IsFailure()) return rc.Miss();

                if (requiredSize != 0)
                    targetMedia = CacheStorageTargetMedia.None;
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

        Result rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Device,
            userId: default, saveDataId: default, index: default);
        if (rc.IsFailure()) return rc.Miss();

        const SaveDataFlags flags = SaveDataFlags.None;

        Result CreateSave() =>
            fs.CreateDeviceSaveData(applicationId, applicationId.Value, saveDataSize, saveDataJournalSize, flags);

        rc = EnsureAndExtendSaveData(fs.Fs, ref requiredSize, in filter, CreateSave, saveDataSize, saveDataJournalSize,
            0x4000);
        if (rc.IsFailure()) return rc.Miss();

        outRequiredSize = requiredSize;
        return Result.Success;
    }

    public static Result GetCacheStorageTargetMedia(this FileSystemClient fs, out CacheStorageTargetMedia targetMedia,
        Ncm.ApplicationId applicationId)
    {
        Result rc = GetCacheStorageTargetMediaImpl(fs, out targetMedia, applicationId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        out CacheStorageTargetMedia targetMedia, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
        long cacheStorageSize, long cacheStorageJournalSize, bool allowExisting)
    {
        outRequiredSize = 0;

        Result rc = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out targetMedia, applicationId,
            saveDataOwnerId, index, cacheStorageSize, cacheStorageJournalSize, allowExisting);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty)
    {
        outRequiredSize = 0;

        Result rc = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out _, applicationId,
            controlProperty.SaveDataOwnerId, index: 0, controlProperty.CacheStorageSize,
            controlProperty.CacheStorageJournalSize, true);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

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
            Result rc = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out targetMedia, applicationId,
                controlProperty.SaveDataOwnerId, index: 0, controlProperty.CacheStorageSize,
                controlProperty.CacheStorageJournalSize, true);

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();
        }

        return Result.Success;
    }

    public static Result CreateApplicationCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        out CacheStorageTargetMedia targetMedia, Ncm.ApplicationId applicationId,
        in ApplicationControlProperty controlProperty, ushort index, long cacheStorageSize,
        long cacheStorageJournalSize)
    {
        UnsafeHelpers.SkipParamInit(out outRequiredSize, out targetMedia);

        Result rc;

        if (index > controlProperty.CacheStorageIndexMax)
        {
            rc = ResultFs.CacheStorageIndexTooLarge.Value;
            fs.Impl.AbortIfNeeded(rc);
            return rc.Miss();
        }

        if (cacheStorageSize + cacheStorageJournalSize > controlProperty.CacheStorageDataAndJournalSizeMax)
        {
            rc = ResultFs.CacheStorageSizeTooLarge.Value;
            fs.Impl.AbortIfNeeded(rc);
            return rc.Miss();
        }

        rc = EnsureApplicationCacheStorageImpl(fs, ref outRequiredSize, out targetMedia, applicationId,
            controlProperty.SaveDataOwnerId, index, cacheStorageSize, cacheStorageJournalSize, false);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result CleanUpTemporaryStorage(this FileSystemClient fs)
    {
        while (true)
        {
            Result rc = SaveDataFilter.Make(out SaveDataFilter filter, programId: default, SaveDataType.Temporary,
                userId: default, saveDataId: default, index: default);

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();

            // Try to find any temporary save data.
            rc = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.Temporary, in filter);

            if (rc.IsFailure())
            {
                if (ResultFs.TargetNotFound.Includes(rc))
                {
                    // No more save data found. We're done cleaning.
                    return Result.Success;
                }

                fs.Impl.AbortIfNeeded(rc);
                return rc.Miss();
            }

            // Delete the found save data.
            rc = fs.Impl.DeleteSaveData(SaveDataSpaceId.Temporary, info.SaveDataId);
            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();
        }
    }

    public static Result EnsureApplicationBcatDeliveryCacheStorage(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty)
    {
        outRequiredSize = 0;

        Result rc = EnsureApplicationBcatDeliveryCacheStorageImpl(fs, ref outRequiredSize, applicationId,
            in controlProperty);

        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result EnsureApplicationSaveData(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty, in Uid user)
    {
        UnsafeHelpers.SkipParamInit(out outRequiredSize);

        using var prohibiter = new UniqueRef<SaveDataTransferProhibiterForCloudBackUp>();
        Result rc = fs.Impl.OpenSaveDataTransferProhibiterForCloudBackUp(ref prohibiter.Ref(), applicationId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

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
            rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Account, userId,
                saveDataId: default, index: default);

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();

            long baseSize = RoundUpOccupationSize(new SaveDataMetaPolicy(SaveDataType.Account).GetSaveDataMetaSize()) +
                            0x8000;

            rc = EnsureAndExtendSaveData(fs, ref requiredSize, in filter, CreateAccountSaveFunc, accountSaveDataSize,
                accountSaveJournalSize, baseSize);

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();
        }

        // Ensure the device save exists
        if (controlProperty.DeviceSaveDataSize > 0)
        {
            Result CreateDeviceSaveFunc() => fs.Impl.CreateDeviceSaveData(applicationId, saveDataOwnerId,
                deviceSaveDataSize, deviceSaveJournalSize, SaveDataFlags.None);

            rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Device,
                userId: default, saveDataId: default, index: default);

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();

            long baseSize = RoundUpOccupationSize(new SaveDataMetaPolicy(SaveDataType.Device).GetSaveDataMetaSize()) +
                            0x8000;

            long requiredSizeForDeviceSaveData = 0;
            rc = EnsureAndExtendSaveData(fs, ref requiredSizeForDeviceSaveData, in filter, CreateDeviceSaveFunc,
                deviceSaveDataSize, deviceSaveJournalSize, baseSize);

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc.Miss();

            if (requiredSizeForDeviceSaveData == 0)
            {
                rc = fs.Impl.CreateDeviceSaveData(applicationId, saveDataOwnerId, deviceSaveDataSize,
                    deviceSaveJournalSize, SaveDataFlags.None);

                if (rc.IsFailure())
                {
                    if (ResultFs.PathAlreadyExists.Includes(rc))
                    {
                        // Nothing to do if the save already exists.
                    }
                    else if (ResultFs.UsableSpaceNotEnough.Includes(rc))
                    {
                        requiredSizeForDeviceSaveData +=
                            RoundUpOccupationSize(new SaveDataMetaPolicy(SaveDataType.Device).GetSaveDataMetaSize()) +
                            0x4000;
                    }
                    else
                    {
                        return rc.Miss();
                    }
                }
            }

            requiredSize += requiredSizeForDeviceSaveData;
        }

        long requiredSizeForBcat = 0;
        rc = EnsureApplicationBcatDeliveryCacheStorageImpl(fs, ref requiredSizeForBcat, applicationId,
            in controlProperty);

        if (rc.IsFailure())
        {
            if (ResultFs.UsableSpaceNotEnough.Includes(rc))
            {
                requiredSize += requiredSizeForBcat;
            }
            else
            {
                return rc.Miss();
            }
        }

        if (controlProperty.TemporaryStorageSize > 0)
        {
            static Result CalculateRequiredSizeForTemporaryStorage(FileSystemClient fs, out long requiredSize,
                in ApplicationControlProperty controlProperty)
            {
                UnsafeHelpers.SkipParamInit(out requiredSize);

                Result rc = fs.Impl.QuerySaveDataTotalSize(out long saveDataTotalSize,
                    controlProperty.TemporaryStorageSize, saveDataJournalSize: 0);
                if (rc.IsFailure()) return rc.Miss();

                requiredSize = RoundUpOccupationSize(saveDataTotalSize) + 0x4000;
                return Result.Success;
            }

            if (requiredSize == 0)
            {
                rc = fs.Impl.CreateTemporaryStorage(applicationId, saveDataOwnerId,
                    controlProperty.TemporaryStorageSize, SaveDataFlags.None);

                if (rc.IsFailure())
                {
                    if (ResultFs.UsableSpaceNotEnough.Includes(rc))
                    {
                        rc = CalculateRequiredSizeForTemporaryStorage(fs, out long temporaryStorageSize,
                            in controlProperty);

                        fs.Impl.AbortIfNeeded(rc);
                        if (rc.IsFailure()) return rc.Miss();

                        requiredSize += temporaryStorageSize;
                    }
                    else
                    {
                        return rc.Miss();
                    }
                }
            }
            else
            {
                // If there was already insufficient space to create the previous saves, check if the temp
                // save already exists instead of trying to create a new one.
                rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, SaveDataType.Temporary,
                    userId: default, saveDataId: default, index: default);

                fs.Impl.AbortIfNeeded(rc);
                if (rc.IsFailure()) return rc.Miss();

                // Check if the temporary save exists
                rc = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo _, SaveDataSpaceId.Temporary, in filter);

                if (rc.IsFailure())
                {
                    if (ResultFs.TargetNotFound.Includes(rc))
                    {
                        // If it doesn't exist, calculate the size required to create it
                        rc = CalculateRequiredSizeForTemporaryStorage(fs, out long temporaryStorageSize,
                            in controlProperty);

                        fs.Impl.AbortIfNeeded(rc);
                        if (rc.IsFailure()) return rc.Miss();

                        requiredSize += temporaryStorageSize;
                    }
                    else
                    {
                        return rc.Miss();
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
        return ResultFs.UsableSpaceNotEnough.Log();
    }

    public static Result ExtendApplicationSaveData(this FileSystemClient fs, out long outRequiredSize,
        Ncm.ApplicationId applicationId, in ApplicationControlProperty controlProperty, SaveDataType type, in Uid user,
        long saveDataSize, long saveDataJournalSize)
    {
        UnsafeHelpers.SkipParamInit(out outRequiredSize);

        Result rc = CheckSaveDataType(type, in user);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        UserId userId = Unsafe.As<Uid, UserId>(ref Unsafe.AsRef(in user));
        rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, type, userId, saveDataId: default,
            index: default);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = CheckExtensionSizeUnderMax(type, in controlProperty, saveDataSize, saveDataJournalSize);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        long requiredSize = 0;
        rc = ExtendSaveDataIfNeeded(fs, ref requiredSize, SaveDataSpaceId.User, info.SaveDataId, saveDataSize,
            saveDataJournalSize);

        if (rc.IsFailure())
        {
            if (ResultFs.UsableSpaceNotEnough.Includes(rc))
            {
                outRequiredSize = requiredSize;

                fs.Impl.AbortIfNeeded(rc);
                return rc.Rethrow();
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc.Miss();
        }

        return Result.Success;
    }

    public static Result GetApplicationSaveDataSize(this FileSystemClient fs, out long outSaveDataSize,
        out long outSaveDataJournalSize, Ncm.ApplicationId applicationId, SaveDataType type, in Uid user)
    {
        UnsafeHelpers.SkipParamInit(out outSaveDataSize, out outSaveDataJournalSize);

        Result rc = CheckSaveDataType(type, in user);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        UserId userId = Unsafe.As<Uid, UserId>(ref Unsafe.AsRef(in user));
        rc = SaveDataFilter.Make(out SaveDataFilter filter, applicationId.Value, type, userId, saveDataId: default,
            index: default);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = fs.Impl.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = fs.Impl.GetSaveDataAvailableSize(out long saveDataSize, info.SaveDataId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        rc = fs.Impl.GetSaveDataJournalSize(out long saveDataJournalSize, info.SaveDataId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        outSaveDataSize = saveDataSize;
        outSaveDataJournalSize = saveDataJournalSize;

        return Result.Success;
    }
}