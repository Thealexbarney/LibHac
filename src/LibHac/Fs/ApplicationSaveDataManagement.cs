using System.Diagnostics.CodeAnalysis;
using LibHac.Account;
using LibHac.Fs.Shim;
using LibHac.FsSystem.Save;
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

            if (uid != Uid.Zero && nacp.UserAccountSaveDataSize > 0)
            {
                var filter = new SaveDataFilter();
                filter.SetTitleId(applicationId);
                filter.SetSaveDataType(SaveDataType.SaveData);
                filter.SetUserId(new UserId(uid.Id.High, uid.Id.Low));

                Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, ref filter);

                if (rc.IsSuccess())
                {
                    rc = ExtendSaveDataIfNeeded(fs, out long requiredSizeUser, SaveDataSpaceId.User,
                        saveDataInfo.SaveDataId, nacp.UserAccountSaveDataSize, nacp.UserAccountSaveDataJournalSize);

                    if (rc.IsFailure())
                    {
                        if (!ResultRangeFs.InsufficientFreeSpace.Contains(rc))
                        {
                            return rc;
                        }

                        requiredSizeSum = requiredSizeUser;
                    }
                }
                else if (rc != ResultFs.TargetNotFound)
                {
                    return rc;
                }
                else
                {
                    UserId userId = ConvertAccountUidToFsUserId(uid);

                    Result createRc = fs.CreateSaveData(applicationId, userId, nacp.SaveDataOwnerId,
                        nacp.UserAccountSaveDataSize, nacp.UserAccountSaveDataJournalSize, 0);

                    if (createRc.IsFailure())
                    {
                        if (ResultRangeFs.InsufficientFreeSpace.Contains(createRc))
                        {
                            // todo: Call QuerySaveDataTotalSize and assign the value to requiredSizeSum
                            requiredSizeSum = 0;
                        }
                        else if (createRc == ResultFs.PathAlreadyExists)
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
                filter.SetTitleId(applicationId);
                filter.SetSaveDataType(SaveDataType.DeviceSaveData);

                Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, ref filter);

                if (rc.IsSuccess())
                {
                    rc = ExtendSaveDataIfNeeded(fs, out long requiredSizeDevice, SaveDataSpaceId.User,
                        saveDataInfo.SaveDataId, nacp.DeviceSaveDataSize, nacp.DeviceSaveDataJournalSize);

                    if (rc.IsFailure())
                    {
                        if (!ResultRangeFs.InsufficientFreeSpace.Contains(rc))
                        {
                            return rc;
                        }

                        requiredSizeSum += requiredSizeDevice;
                    }
                }
                else if (rc != ResultFs.TargetNotFound)
                {
                    return rc;
                }
                else
                {
                    Result createRc = fs.CreateDeviceSaveData(applicationId, nacp.SaveDataOwnerId,
                        nacp.DeviceSaveDataSize, nacp.DeviceSaveDataJournalSize, 0);

                    if (createRc.IsFailure())
                    {
                        if (ResultRangeFs.InsufficientFreeSpace.Contains(createRc))
                        {
                            // todo: Call QuerySaveDataTotalSize and add the value to requiredSizeSum
                            requiredSizeSum += 0;
                        }
                        else if (createRc == ResultFs.PathAlreadyExists)
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
                if (!ResultRangeFs.InsufficientFreeSpace.Contains(bcatRc))
                {
                    return bcatRc;
                }

                requiredSizeSum += requiredSizeBcat;
            }

            // Don't actually do this yet because the temp save indexer hasn't been implemented
            // todo: Flip the operator when it is
            if (nacp.TemporaryStorageSize < 0)
            {
                if (requiredSizeSum > 0)
                {
                    var filter = new SaveDataFilter();
                    filter.SetTitleId(applicationId);
                    filter.SetSaveDataType(SaveDataType.TemporaryStorage);

                    Result rc = fs.FindSaveDataWithFilter(out _, SaveDataSpaceId.User, ref filter);

                    if (rc.IsFailure())
                    {
                        if(rc != ResultFs.TargetNotFound)
                        {
                            return rc;
                        }

                        // todo: Call QuerySaveDataTotalSize and add the value to requiredSizeSum
                        requiredSizeSum += 0;
                    }
                }
                else
                {
                    Result createRc = fs.CreateTemporaryStorage(applicationId, nacp.SaveDataOwnerId,
                        nacp.TemporaryStorageSize, 0);

                    if (createRc.IsFailure())
                    {
                        if (ResultRangeFs.InsufficientFreeSpace.Contains(createRc))
                        {
                            // todo: Call QuerySaveDataTotalSize and assign the value to requiredSizeSum
                            requiredSizeSum += 0;
                        }
                        else if (createRc == ResultFs.PathAlreadyExists)
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
            filter.SetTitleId(applicationId);
            filter.SetSaveDataType(SaveDataType.BcatDeliveryCacheStorage);

            Result rc = fs.FindSaveDataWithFilter(out SaveDataInfo saveDataInfo, SaveDataSpaceId.User, ref filter);

            if (rc.IsSuccess())
            {
                rc = ExtendSaveDataIfNeeded(fs, out long requiredSizeBcat, SaveDataSpaceId.User,
                    saveDataInfo.SaveDataId, nacp.DeviceSaveDataSize, bcatDeliveryCacheJournalSize);

                if (rc.IsFailure())
                {
                    if (!ResultRangeFs.InsufficientFreeSpace.Contains(rc))
                    {
                        return rc;
                    }

                    requiredSize = requiredSizeBcat;
                }
            }
            else if (rc != ResultFs.TargetNotFound)
            {
                return rc;
            }
            else
            {
                Result createRc = fs.CreateBcatSaveData(applicationId, nacp.BcatDeliveryCacheStorageSize);

                if (createRc.IsFailure())
                {
                    if (ResultRangeFs.InsufficientFreeSpace.Contains(createRc))
                    {
                        // todo: Call QuerySaveDataTotalSize and assign the value to requiredSize
                        requiredSize = 0;
                    }
                    else if (createRc == ResultFs.PathAlreadyExists)
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

        public static UserId ConvertAccountUidToFsUserId(Uid uid)
        {
            return new UserId(uid.Id.High, uid.Id.Low);
        }
    }
}
