using System;
using System.Diagnostics.CodeAnalysis;
using LibHac.Account;
using LibHac.Common;
using LibHac.Fs.Shim;
using LibHac.Ncm;
using LibHac.Ns;
using LibHac.Util;

namespace LibHac.Fs
{
    public static class ApplicationSaveDataManagement
    {
        public static Result EnsureApplicationSaveData(FileSystemClient fs, out long requiredSize, Ncm.ApplicationId applicationId,
            ref ApplicationControlProperty nacp, ref Uid uid)
        {
            UnsafeHelpers.SkipParamInit(out requiredSize);
            long requiredSizeSum = 0;

            // Create local variable for use in closures
            ProgramId saveDataOwnerId = nacp.SaveDataOwnerId;

            // Ensure the user account save exists
            if (uid != Uid.Zero && nacp.UserAccountSaveDataSize > 0)
            {
                // More local variables for use in closures
                Uid uidLocal = uid;
                long accountSaveDataSize = nacp.UserAccountSaveDataSize;
                long accountSaveJournalSize = nacp.UserAccountSaveDataJournalSize;

                Result CreateAccountSaveFunc()
                {
                    UserId userId = ConvertAccountUidToFsUserId(uidLocal);
                    return fs.CreateSaveData(applicationId, userId, saveDataOwnerId.Value, accountSaveDataSize,
                        accountSaveJournalSize, SaveDataFlags.None);
                }

                var filter = new SaveDataFilter();
                filter.SetProgramId(applicationId);
                filter.SetSaveDataType(SaveDataType.Account);
                filter.SetUserId(new UserId(uid.Id.High, uid.Id.Low));

                // The 0x4c000 includes the save meta and other stuff
                Result rc = EnsureAndExtendSaveData(fs, CreateAccountSaveFunc, ref requiredSizeSum, ref filter, 0x4c000,
                    accountSaveDataSize, accountSaveJournalSize);

                if (rc.IsFailure()) return rc;
            }

            // Ensure the device save exists
            if (nacp.DeviceSaveDataSize > 0)
            {
                long deviceSaveDataSize = nacp.DeviceSaveDataSize;
                long deviceSaveJournalSize = nacp.DeviceSaveDataJournalSize;

                Result CreateDeviceSaveFunc() => fs.CreateDeviceSaveData(applicationId, saveDataOwnerId.Value,
                    deviceSaveDataSize, deviceSaveJournalSize, 0);

                var filter = new SaveDataFilter();
                filter.SetProgramId(applicationId);
                filter.SetSaveDataType(SaveDataType.Device);

                Result rc = EnsureAndExtendSaveData(fs, CreateDeviceSaveFunc, ref requiredSizeSum, ref filter, 0x4000,
                    deviceSaveDataSize, deviceSaveJournalSize);

                if (rc.IsFailure()) return rc;
            }

            Result bcatRc = EnsureApplicationBcatDeliveryCacheStorageImpl(fs,
                out long requiredSizeBcat, applicationId, ref nacp);

            if (bcatRc.IsFailure())
            {
                if (!ResultFs.UsableSpaceNotEnough.Includes(bcatRc))
                {
                    return bcatRc;
                }

                requiredSizeSum += requiredSizeBcat;
            }

            if (nacp.TemporaryStorageSize > 0)
            {
                if (requiredSizeSum > 0)
                {
                    // If there was already insufficient space to create the previous saves, check if the temp
                    // save already exists instead of trying to create a new one.
                    var filter = new SaveDataFilter();
                    filter.SetProgramId(applicationId);
                    filter.SetSaveDataType(SaveDataType.Temporary);

                    Result rc = fs.FindSaveDataWithFilter(out _, SaveDataSpaceId.Temporary, in filter);

                    if (rc.IsFailure())
                    {
                        if (!ResultFs.TargetNotFound.Includes(rc))
                        {
                            return rc;
                        }

                        Result queryRc = fs.QuerySaveDataTotalSize(out long tempSaveTotalSize,
                            nacp.TemporaryStorageSize, 0);

                        if (queryRc.IsFailure()) return queryRc;

                        requiredSizeSum += Alignment.AlignUp(tempSaveTotalSize, 0x4000) + 0x4000;
                    }
                }
                else
                {
                    Result createRc = fs.CreateTemporaryStorage(applicationId, nacp.SaveDataOwnerId.Value,
                        nacp.TemporaryStorageSize, 0);

                    if (createRc.IsFailure())
                    {
                        if (ResultFs.UsableSpaceNotEnough.Includes(createRc))
                        {
                            Result queryRc = fs.QuerySaveDataTotalSize(out long tempSaveTotalSize,
                                nacp.TemporaryStorageSize, 0);

                            if (queryRc.IsFailure()) return queryRc;

                            requiredSizeSum += Alignment.AlignUp(tempSaveTotalSize, 0x4000) + 0x4000;
                        }
                        else if (ResultFs.PathAlreadyExists.Includes(createRc))
                        {
                            requiredSizeSum += 0;
                        }
                        else
                        {
                            return createRc;
                        }
                    }
                }
            }

            requiredSize = requiredSizeSum;

            return requiredSize == 0 ? Result.Success : ResultFs.UsableSpaceNotEnough.Log();
        }

        [SuppressMessage("ReSharper", "UnusedParameter.Local")]
        private static Result ExtendSaveDataIfNeeded(FileSystemClient fs, out long requiredSize,
            SaveDataSpaceId spaceId, ulong saveDataId, long requiredDataSize, long requiredJournalSize)
        {
            // Checks the current save data size and extends it if needed.
            // If there is not enough space to do the extension, the amount of space
            // the extension requires will be written to requiredSize.

            requiredSize = 0;
            return Result.Success;
        }

        private static Result CreateSaveData(FileSystemClient fs, Func<Result> createFunc, ref long requiredSize, long baseSize,
            long dataSize, long journalSize)
        {
            Result rc = createFunc();

            if (rc.IsSuccess())
                return Result.Success;

            if (ResultFs.UsableSpaceNotEnough.Includes(rc))
            {
                Result queryRc = fs.QuerySaveDataTotalSize(out long totalSize, dataSize, journalSize);
                if (queryRc.IsFailure()) return queryRc;

                requiredSize += Alignment.AlignUp(totalSize, 0x4000) + baseSize;
            }
            else if (!ResultFs.PathAlreadyExists.Includes(rc))
            {
                return rc;
            }

            return Result.Success;
        }

        private static Result EnsureAndExtendSaveData(FileSystemClient fs, Func<Result> createFunc,
            ref long requiredSize, ref SaveDataFilter filter, long baseSize, long dataSize, long journalSize)
        {
            Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo info, SaveDataSpaceId.User, in filter);

            if (rc.IsFailure())
            {
                if (ResultFs.TargetNotFound.Includes(rc))
                {
                    rc = CreateSaveData(fs, createFunc, ref requiredSize, baseSize, dataSize, journalSize);
                }

                return rc;
            }

            rc = ExtendSaveDataIfNeeded(fs, out long requiredSizeExtend, SaveDataSpaceId.User, info.SaveDataId,
                dataSize, journalSize);

            if (rc.IsFailure())
            {
                if (!ResultFs.UsableSpaceNotEnough.Includes(rc))
                    return rc;

                requiredSize += requiredSizeExtend;
            }

            return Result.Success;
        }

        private static Result EnsureApplicationBcatDeliveryCacheStorageImpl(FileSystemClient fs, out long requiredSize,
            Ncm.ApplicationId applicationId, ref ApplicationControlProperty nacp)
        {
            const long bcatDeliveryCacheJournalSize = 0x200000;

            long bcatStorageSize = nacp.BcatDeliveryCacheStorageSize;
            if (bcatStorageSize <= 0)
            {
                requiredSize = 0;
                return Result.Success;
            }

            UnsafeHelpers.SkipParamInit(out requiredSize);
            long requiredSizeBcat = 0;

            var filter = new SaveDataFilter();
            filter.SetProgramId(applicationId);
            filter.SetSaveDataType(SaveDataType.Bcat);

            Result CreateBcatStorageFunc() => fs.CreateBcatSaveData(applicationId, bcatStorageSize);

            Result rc = EnsureAndExtendSaveData(fs, CreateBcatStorageFunc,
                ref requiredSizeBcat, ref filter, 0x4000, bcatStorageSize, bcatDeliveryCacheJournalSize);

            if (rc.IsFailure()) return rc;

            requiredSize = requiredSizeBcat;
            return requiredSizeBcat > 0 ? ResultFs.UsableSpaceNotEnough.Log() : Result.Success;
        }

        private static Result EnsureApplicationCacheStorageImpl(this FileSystemClient fs, out long requiredSize,
            out CacheStorageTargetMedia target, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
            long dataSize, long journalSize, bool allowExisting)
        {
            UnsafeHelpers.SkipParamInit(out requiredSize);
            target = CacheStorageTargetMedia.SdCard;

            Result rc = fs.GetCacheStorageTargetMediaImpl(out CacheStorageTargetMedia targetMedia, applicationId);
            if (rc.IsFailure()) return rc;

            long requiredSizeLocal = 0;

            if (targetMedia == CacheStorageTargetMedia.Nand)
            {
                rc = TryCreateCacheStorage(fs, out requiredSizeLocal, SaveDataSpaceId.User, applicationId,
                    saveDataOwnerId, index, dataSize, journalSize, allowExisting);
                if (rc.IsFailure()) return rc;
            }
            else if (targetMedia == CacheStorageTargetMedia.SdCard)
            {
                rc = TryCreateCacheStorage(fs, out requiredSizeLocal, SaveDataSpaceId.SdCache, applicationId,
                    saveDataOwnerId, index, dataSize, journalSize, allowExisting);
                if (rc.IsFailure()) return rc;
            }
            // Savedata doesn't exist. Try to create a new one.
            else
            {
                // Try to create the savedata on the SD card first
                if (fs.IsSdCardAccessible())
                {
                    target = CacheStorageTargetMedia.SdCard;

                    Result CreateFuncSdCard() => fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache,
                        saveDataOwnerId, index, dataSize, journalSize, SaveDataFlags.None);

                    rc = CreateSaveData(fs, CreateFuncSdCard, ref requiredSizeLocal, 0x4000, dataSize, journalSize);
                    if (rc.IsFailure()) return rc;

                    if (requiredSizeLocal == 0)
                    {
                        requiredSize = 0;
                        return Result.Success;
                    }
                }

                // If the save can't be created on the SD card, try creating it on the User BIS partition
                requiredSizeLocal = 0;
                target = CacheStorageTargetMedia.Nand;

                Result CreateFuncNand() => fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, saveDataOwnerId,
                    index, dataSize, journalSize, SaveDataFlags.None);

                rc = CreateSaveData(fs, CreateFuncNand, ref requiredSizeLocal, 0x4000, dataSize, journalSize);
                if (rc.IsFailure()) return rc;

                if (requiredSizeLocal != 0)
                {
                    target = CacheStorageTargetMedia.None;
                    requiredSize = requiredSizeLocal;
                    return ResultFs.UsableSpaceNotEnough.Log();
                }
            }

            requiredSize = 0;
            return Result.Success;
        }

        public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long requiredSize,
            out CacheStorageTargetMedia target, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
            long dataSize, long journalSize, bool allowExisting)
        {
            return EnsureApplicationCacheStorageImpl(fs, out requiredSize, out target, applicationId, saveDataOwnerId,
                index, dataSize, journalSize, allowExisting);
        }

        public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long requiredSize,
            Ncm.ApplicationId applicationId, ref ApplicationControlProperty nacp)
        {
            return EnsureApplicationCacheStorageImpl(fs, out requiredSize, out _, applicationId, nacp.SaveDataOwnerId.Value,
                0, nacp.CacheStorageSize, nacp.CacheStorageJournalSize, true);
        }

        public static Result EnsureApplicationCacheStorage(this FileSystemClient fs, out long requiredSize,
            out CacheStorageTargetMedia target, Ncm.ApplicationId applicationId, ref ApplicationControlProperty nacp)
        {
            UnsafeHelpers.SkipParamInit(out requiredSize, out target);

            if (nacp.CacheStorageSize <= 0)
                return Result.Success;

            return EnsureApplicationCacheStorageImpl(fs, out requiredSize, out target, applicationId,
                nacp.SaveDataOwnerId.Value, 0, nacp.CacheStorageSize, nacp.CacheStorageJournalSize, true);

        }

        public static Result CreateApplicationCacheStorage(this FileSystemClient fs, out long requiredSize,
            out CacheStorageTargetMedia target, Ncm.ApplicationId applicationId, ref ApplicationControlProperty nacp,
            ushort index, long dataSize, long journalSize)
        {
            UnsafeHelpers.SkipParamInit(out requiredSize, out target);

            if (index > nacp.CacheStorageMaxIndex)
                return ResultFs.CacheStorageIndexTooLarge.Log();

            if (dataSize + journalSize > nacp.CacheStorageMaxSizeAndMaxJournalSize)
                return ResultFs.CacheStorageSizeTooLarge.Log();

            Result rc = fs.EnsureApplicationCacheStorage(out requiredSize, out target, applicationId,
                nacp.SaveDataOwnerId.Value, index, dataSize, journalSize, false);

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result EnsureApplicationBcatDeliveryCacheStorage(this FileSystemClient fs, out long requiredSize,
            Ncm.ApplicationId applicationId, ref ApplicationControlProperty nacp)
        {
            return EnsureApplicationBcatDeliveryCacheStorageImpl(fs, out requiredSize, applicationId, ref nacp);
        }

        public static Result TryCreateCacheStorage(this FileSystemClient fs, out long requiredSize,
            SaveDataSpaceId spaceId, Ncm.ApplicationId applicationId, ulong saveDataOwnerId, ushort index,
            long dataSize, long journalSize, bool allowExisting)
        {
                UnsafeHelpers.SkipParamInit(out requiredSize);
            long requiredSizeLocal = 0;

            var filter = new SaveDataFilter();
            filter.SetProgramId(applicationId);
            filter.SetIndex(index);
            filter.SetSaveDataType(SaveDataType.Cache);

            Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo info, spaceId, in filter);

            if (rc.IsFailure())
            {
                if (!ResultFs.TargetNotFound.Includes(rc))
                    return rc;

                Result CreateCacheFunc() => fs.CreateCacheStorage(applicationId, spaceId, saveDataOwnerId, index,
                    dataSize, journalSize, SaveDataFlags.None);

                rc = CreateSaveData(fs, CreateCacheFunc, ref requiredSizeLocal, 0x4000, dataSize, journalSize);
                if (rc.IsFailure()) return rc;

                requiredSize = requiredSizeLocal;
                return Result.Success;
            }

            if (!allowExisting)
            {
                return ResultFs.AlreadyExists.Log();
            }

            rc = ExtendSaveDataIfNeeded(fs, out requiredSizeLocal, spaceId, info.SaveDataId, dataSize, journalSize);

            if (rc.IsSuccess() || ResultFs.UsableSpaceNotEnough.Includes(rc))
            {
                requiredSize = requiredSizeLocal;
                return Result.Success;
            }

            if (ResultFs.SaveDataExtending.Includes(rc))
            {
                return ResultFs.SaveDataCorrupted.LogConverted(rc);
            }

            return rc;
        }

        public static Result GetCacheStorageTargetMedia(this FileSystemClient fs, out CacheStorageTargetMedia target,
            Ncm.ApplicationId applicationId)
        {
            return GetCacheStorageTargetMediaImpl(fs, out target, applicationId);
        }

        private static Result GetCacheStorageTargetMediaImpl(this FileSystemClient fs,
            out CacheStorageTargetMedia target, Ncm.ApplicationId applicationId)
        {
            target = CacheStorageTargetMedia.None;

            var filter = new SaveDataFilter();
            filter.SetProgramId(applicationId);
            filter.SetSaveDataType(SaveDataType.Cache);

            if (fs.IsSdCardAccessible())
            {
                Result rc = fs.FindSaveDataWithFilter(out _, SaveDataSpaceId.SdCache, in filter);
                if (rc.IsFailure() && !ResultFs.TargetNotFound.Includes(rc)) return rc;

                if (rc.IsSuccess())
                {
                    target = CacheStorageTargetMedia.SdCard;
                }
            }

            // Not on the SD card. Check it it's in NAND
            if (target == CacheStorageTargetMedia.None)
            {
                Result rc = fs.FindSaveDataWithFilter(out _, SaveDataSpaceId.User, in filter);
                if (rc.IsFailure() && !ResultFs.TargetNotFound.Includes(rc)) return rc;

                if (rc.IsSuccess())
                {
                    target = CacheStorageTargetMedia.Nand;
                }
            }

            return Result.Success;
        }

        public static Result CleanUpTemporaryStorage(FileSystemClient fs)
        {
            var filter = new SaveDataFilter();
            filter.SetSaveDataType(SaveDataType.Temporary);

            Result rc;

            while (true)
            {
                rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveInfo, SaveDataSpaceId.Temporary, in filter);

                if (rc.IsFailure()) break;

                rc = fs.DeleteSaveData(SaveDataSpaceId.Temporary, saveInfo.SaveDataId);
                if (rc.IsFailure()) return rc;
            }

            if (ResultFs.TargetNotFound.Includes(rc))
                return Result.Success;

            return rc;
        }

        public static UserId ConvertAccountUidToFsUserId(Uid uid)
        {
            return new UserId(uid.Id.High, uid.Id.Low);
        }
    }
}
