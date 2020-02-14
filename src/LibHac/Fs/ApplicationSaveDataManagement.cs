using System.Diagnostics.CodeAnalysis;
using LibHac.Account;
using LibHac.Fs.Shim;
using LibHac.Ncm;
using LibHac.Ns;

namespace LibHac.Fs
{
    public static class ApplicationSaveDataManagement
    {
        public static Result EnsureApplicationSaveData(FileSystemClient fs, out long requiredSize, TitleId applicationId,
            ref ApplicationControlProperty nacp, ref Uid uid)
        {
            requiredSize = default;

            long requiredSizeSum = 0;

            // If the application needs a user save
            if (uid != Uid.Zero && nacp.UserAccountSaveDataSize > 0)
            {
                var filter = new SaveDataFilter();
                filter.SetProgramId(applicationId);
                filter.SetSaveDataType(SaveDataType.Account);
                filter.SetUserId(new UserId(uid.Id.High, uid.Id.Low));

                Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, ref filter);

                // If the save already exists
                if (rc.IsSuccess())
                {
                    // Make sure the save is large enough
                    rc = ExtendSaveDataIfNeeded(fs, out long requiredSizeUser, SaveDataSpaceId.User,
                        saveDataInfo.SaveDataId, nacp.UserAccountSaveDataSize, nacp.UserAccountSaveDataJournalSize);

                    if (rc.IsFailure())
                    {
                        if (!ResultFs.InsufficientFreeSpace.Includes(rc))
                        {
                            return rc;
                        }

                        requiredSizeSum = requiredSizeUser;
                    }
                }
                else if (!ResultFs.TargetNotFound.Includes(rc))
                {
                    return rc;
                }
                else
                {
                    // The save doesn't exist, so try to create it
                    UserId userId = ConvertAccountUidToFsUserId(uid);

                    Result createRc = fs.CreateSaveData(applicationId, userId, nacp.SaveDataOwnerId,
                        nacp.UserAccountSaveDataSize, nacp.UserAccountSaveDataJournalSize, 0);

                    if (createRc.IsFailure())
                    {
                        // If there's insufficient free space, calculate the space required to create the save
                        if (ResultFs.InsufficientFreeSpace.Includes(createRc))
                        {
                            Result queryRc = fs.QuerySaveDataTotalSize(out long userAccountTotalSize,
                                nacp.UserAccountSaveDataSize, nacp.UserAccountSaveDataJournalSize);

                            if (queryRc.IsFailure()) return queryRc;

                            // The 0x4c000 includes the save meta and other stuff
                            requiredSizeSum = Util.AlignUp(userAccountTotalSize, 0x4000) + 0x4c000;
                        }
                        else if (ResultFs.PathAlreadyExists.Includes(createRc))
                        {
                            requiredSizeSum = 0;
                        }
                        else
                        {
                            return createRc;
                        }
                    }
                }
            }

            if (nacp.DeviceSaveDataSize > 0)
            {
                var filter = new SaveDataFilter();
                filter.SetProgramId(applicationId);
                filter.SetSaveDataType(SaveDataType.Device);

                Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, ref filter);

                if (rc.IsSuccess())
                {
                    rc = ExtendSaveDataIfNeeded(fs, out long requiredSizeDevice, SaveDataSpaceId.User,
                        saveDataInfo.SaveDataId, nacp.DeviceSaveDataSize, nacp.DeviceSaveDataJournalSize);

                    if (rc.IsFailure())
                    {
                        if (!ResultFs.InsufficientFreeSpace.Includes(rc))
                        {
                            return rc;
                        }

                        requiredSizeSum += requiredSizeDevice;
                    }
                }
                else if (!ResultFs.TargetNotFound.Includes(rc))
                {
                    return rc;
                }
                else
                {
                    Result createRc = fs.CreateDeviceSaveData(applicationId, nacp.SaveDataOwnerId,
                        nacp.DeviceSaveDataSize, nacp.DeviceSaveDataJournalSize, 0);

                    if (createRc.IsFailure())
                    {
                        if (ResultFs.InsufficientFreeSpace.Includes(createRc))
                        {
                            Result queryRc = fs.QuerySaveDataTotalSize(out long deviceSaveTotalSize,
                                nacp.DeviceSaveDataSize, nacp.DeviceSaveDataJournalSize);

                            if (queryRc.IsFailure()) return queryRc;

                            // Not sure what the additional 0x4000 is
                            requiredSizeSum += Util.AlignUp(deviceSaveTotalSize, 0x4000) + 0x4000;
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

            Result bcatRc = EnsureApplicationBcatDeliveryCacheStorageImpl(fs,
                out long requiredSizeBcat, applicationId, ref nacp);

            if (bcatRc.IsFailure())
            {
                if (!ResultFs.InsufficientFreeSpace.Includes(bcatRc))
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

                    Result rc = fs.FindSaveDataWithFilter(out _, SaveDataSpaceId.Temporary, ref filter);

                    if (rc.IsFailure())
                    {
                        if (!ResultFs.TargetNotFound.Includes(rc))
                        {
                            return rc;
                        }

                        Result queryRc = fs.QuerySaveDataTotalSize(out long tempSaveTotalSize,
                            nacp.TemporaryStorageSize, 0);

                        if (queryRc.IsFailure()) return queryRc;

                        requiredSizeSum += Util.AlignUp(tempSaveTotalSize, 0x4000) + 0x4000;
                    }
                }
                else
                {
                    Result createRc = fs.CreateTemporaryStorage(applicationId, nacp.SaveDataOwnerId,
                        nacp.TemporaryStorageSize, 0);

                    if (createRc.IsFailure())
                    {
                        if (ResultFs.InsufficientFreeSpace.Includes(createRc))
                        {
                            Result queryRc = fs.QuerySaveDataTotalSize(out long tempSaveTotalSize,
                                nacp.TemporaryStorageSize, 0);

                            if (queryRc.IsFailure()) return queryRc;

                            requiredSizeSum += Util.AlignUp(tempSaveTotalSize, 0x4000) + 0x4000;
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

            return requiredSize == 0 ? Result.Success : ResultFs.InsufficientFreeSpace.Log();
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

        private static Result EnsureApplicationBcatDeliveryCacheStorageImpl(FileSystemClient fs, out long requiredSize,
            TitleId applicationId, ref ApplicationControlProperty nacp)
        {
            const long bcatDeliveryCacheJournalSize = 0x200000;

            requiredSize = default;

            if (nacp.BcatDeliveryCacheStorageSize <= 0)
            {
                requiredSize = 0;
                return Result.Success;
            }

            var filter = new SaveDataFilter();
            filter.SetProgramId(applicationId);
            filter.SetSaveDataType(SaveDataType.Bcat);

            Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, ref filter);

            if (rc.IsSuccess())
            {
                rc = ExtendSaveDataIfNeeded(fs, out long requiredSizeBcat, SaveDataSpaceId.User,
                    saveDataInfo.SaveDataId, nacp.BcatDeliveryCacheStorageSize, bcatDeliveryCacheJournalSize);

                if (rc.IsFailure())
                {
                    if (!ResultFs.InsufficientFreeSpace.Includes(rc))
                    {
                        return rc;
                    }

                    requiredSize = requiredSizeBcat;
                }
            }
            else if (!ResultFs.TargetNotFound.Includes(rc))
            {
                return rc;
            }
            else
            {
                Result createRc = fs.CreateBcatSaveData(applicationId, nacp.BcatDeliveryCacheStorageSize);

                if (createRc.IsFailure())
                {
                    if (ResultFs.InsufficientFreeSpace.Includes(createRc))
                    {
                        Result queryRc = fs.QuerySaveDataTotalSize(out long saveTotalSize,
                            nacp.BcatDeliveryCacheStorageSize, bcatDeliveryCacheJournalSize);

                        if (queryRc.IsFailure()) return queryRc;

                        requiredSize = Util.AlignUp(saveTotalSize, 0x4000) + 0x4000;
                    }
                    else if (ResultFs.PathAlreadyExists.Includes(createRc))
                    {
                        requiredSize = 0;
                    }
                    else
                    {
                        return createRc;
                    }
                }
            }

            return requiredSize > 0 ? ResultFs.InsufficientFreeSpace.Log() : Result.Success;
        }

        public static Result CleanUpTemporaryStorage(FileSystemClient fs)
        {
            var filter = new SaveDataFilter();
            filter.SetSaveDataType(SaveDataType.Temporary);

            Result rc;

            while (true)
            {
                rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveInfo, SaveDataSpaceId.Temporary, ref filter);

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
