using LibHac.FsService;
using LibHac.FsSystem.Save;
using LibHac.Ncm;

namespace LibHac.Fs.Shim
{
    public static class SaveDataManagement
    {
        public static Result CreateSaveData(this FileSystemClient fs, TitleId titleId, UserId userId, TitleId ownerId,
            long size, long journalSize, uint flags)
        {
            return fs.RunOperationWithAccessLog(LocalAccessLogMode.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        TitleId = titleId,
                        UserId = userId,
                        Type = SaveDataType.SaveData
                    };

                    var createInfo = new SaveDataCreateInfo
                    {
                        Size = size,
                        JournalSize = journalSize,
                        BlockSize = 0x4000,
                        OwnerId = ownerId,
                        Flags = flags,
                        SpaceId = SaveDataSpaceId.User
                    };

                    var metaInfo = new SaveMetaCreateInfo
                    {
                        Type = SaveMetaType.Thumbnail,
                        Size = 0x40060
                    };

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref createInfo, ref metaInfo);
                },
                () => $", applicationid: 0x{titleId.Value:X}, userid: 0x{userId}, save_data_owner_id: 0x{ownerId.Value:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{flags:x8}");
        }

        public static Result CreateSaveData(this FileSystemClient fs, TitleId titleId, UserId userId, TitleId ownerId,
            long size, long journalSize, HashSalt hashSalt, uint flags)
        {
            return fs.RunOperationWithAccessLog(LocalAccessLogMode.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        TitleId = titleId,
                        UserId = userId,
                        Type = SaveDataType.SaveData
                    };

                    var createInfo = new SaveDataCreateInfo
                    {
                        Size = size,
                        JournalSize = journalSize,
                        BlockSize = 0x4000,
                        OwnerId = ownerId,
                        Flags = flags,
                        SpaceId = SaveDataSpaceId.User
                    };

                    var metaInfo = new SaveMetaCreateInfo
                    {
                        Type = SaveMetaType.Thumbnail,
                        Size = 0x40060
                    };

                    return fsProxy.CreateSaveDataFileSystemWithHashSalt(ref attribute, ref createInfo, ref metaInfo, ref hashSalt);
                },
                () => $", applicationid: 0x{titleId.Value:X}, userid: 0x{userId}, save_data_owner_id: 0x{ownerId.Value:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{flags:x8}");
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, UserId userId, TitleId ownerId, long size, long journalSize, uint flags)
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
                () => $", savedataspaceid: {spaceId}, savedataid: 0x{saveDataId:X}, userid: 0x{userId.Id.High:X16}{userId.Id.Low:X16}, save_data_owner_id: 0x{ownerId.Value:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{flags:X8}");
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId,
            TitleId ownerId, long size, long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId, long size,
            long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, TitleId.Zero, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, TitleId ownerId, long size,
            long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, UserId.Zero, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, long size,
            long journalSize, uint flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, UserId.Zero, TitleId.Zero, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            TitleId ownerId, long size, long journalSize, uint flags)
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
