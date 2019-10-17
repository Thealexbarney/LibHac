using System;
using LibHac.Common;
using LibHac.FsService;
using LibHac.FsSystem.Save;
using LibHac.Ncm;

namespace LibHac.Fs
{
    public static class SaveData
    {
        public static Result MountSaveData(this FileSystemClient fs, U8Span mountName, TitleId titleId, UserId userId)
        {
            Result rc;

            if (fs.IsEnabledAccessLog(LocalAccessLogMode.Application))
            {
                TimeSpan startTime = fs.Time.GetCurrent();
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, titleId, userId, SaveDataType.SaveData, false, 0);
                TimeSpan endTime = fs.Time.GetCurrent();

                fs.OutputAccessLog(rc, startTime, endTime, $", name: \"{mountName.ToString()}\", applicationid: 0x{titleId}, userid: {userId}");
            }
            else
            {
                rc = MountSaveDataImpl(fs, mountName, SaveDataSpaceId.User, titleId, userId, SaveDataType.SaveData, false, 0);
            }

            if (rc.IsSuccess() && fs.IsEnabledAccessLog(LocalAccessLogMode.Application))
            {
                fs.EnableFileSystemAccessorAccessLog(mountName);
            }

            return rc;
        }

        private static Result MountSaveDataImpl(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId,
            TitleId titleId, UserId userId, SaveDataType type, bool openReadOnly, short index)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            SaveDataAttribute attribute = default;
            attribute.TitleId = titleId;
            attribute.UserId = userId;
            attribute.Type = type;
            attribute.Index = index;

            IFileSystem saveFs;

            if (openReadOnly)
            {
                rc = fsProxy.OpenReadOnlySaveDataFileSystem(out saveFs, spaceId, ref attribute);
            }
            else
            {
                rc = fsProxy.OpenSaveDataFileSystem(out saveFs, spaceId, ref attribute);
            }

            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, saveFs);
        }

        public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return MountSystemSaveData(fs, mountName, spaceId, saveDataId, UserId.Zero);
        }

        public static Result MountSystemSaveData(this FileSystemClient fs, U8Span mountName,
            SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
        {
            Result rc = MountHelpers.CheckMountName(mountName);
            if (rc.IsFailure()) return rc;

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            SaveDataAttribute attribute = default;
            attribute.UserId = userId;
            attribute.SaveDataId = saveDataId;

            rc = fsProxy.OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, spaceId, ref attribute);
            if (rc.IsFailure()) return rc;

            return fs.Register(mountName, fileSystem);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, UserId userId, ulong ownerId, long size, long journalSize, uint flags)
        {
            return fs.RunOperationWithAccessLog(LocalAccessLogMode.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        UserId = userId,
                        SaveDataId = saveDataId
                    };

                    var createInfo = new SaveDataCreateInfo
                    {
                        Size = size,
                        JournalSize = journalSize,
                        BlockSize = 0x4000,
                        OwnerId = ownerId,
                        Flags = flags,
                        SpaceId = spaceId
                    };

                    return fsProxy.CreateSaveDataFileSystemBySystemSaveDataId(ref attribute, ref createInfo);
                },
                () => $", savedataspaceid: {spaceId}, savedataid: 0x{saveDataId:X}, userid: 0x{userId.Id.High:X16}{userId.Id.Low:X16}, save_data_owner_id: 0x{ownerId:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{flags:X8}");
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId,
            ulong ownerId, long size, long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId, long size,
            long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, ulong ownerId, long size,
            long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, UserId.Zero, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, long size,
            long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, UserId.Zero, 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            ulong ownerId, long size, long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, spaceId, saveDataId, UserId.Zero, ownerId, size, journalSize, flags);
        }

        public static Result DeleteSaveData(this FileSystemClient fs, ulong saveDataId)
        {
            return fs.RunOperationWithAccessLog(LocalAccessLogMode.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();
                    return fsProxy.DeleteSaveDataFileSystem(saveDataId);
                },
                () => $", savedataid: 0x{saveDataId:X}");
        }

        public static Result DeleteSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return fs.RunOperationWithAccessLog(LocalAccessLogMode.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();
                    return fsProxy.DeleteSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);
                },
                () => $", savedataspaceid: {spaceId}, savedataid: 0x{saveDataId:X}");
        }

        public static Result DisableAutoSaveDataCreation(this FileSystemClient fsClient)
        {
            IFileSystemProxy fsProxy = fsClient.GetFileSystemProxyServiceObject();

            return fsProxy.DisableAutoSaveDataCreation();
        }
    }
}
