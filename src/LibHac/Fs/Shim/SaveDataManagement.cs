using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.FsService;
using LibHac.FsSystem.Save;
using LibHac.Ncm;

namespace LibHac.Fs.Shim
{
    public static class SaveDataManagement
    {
        public static Result CreateSaveData(this FileSystemClient fs, TitleId applicationId, UserId userId, TitleId ownerId,
            long size, long journalSize, uint flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        TitleId = applicationId,
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
                () => $", applicationid: 0x{applicationId.Value:X}, userid: 0x{userId}, save_data_owner_id: 0x{ownerId.Value:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{flags:x8}");
        }

        public static Result CreateSaveData(this FileSystemClient fs, TitleId applicationId, UserId userId, TitleId ownerId,
            long size, long journalSize, HashSalt hashSalt, uint flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        TitleId = applicationId,
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
                () => $", applicationid: 0x{applicationId.Value:X}, userid: 0x{userId}, save_data_owner_id: 0x{ownerId.Value:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{flags:x8}");
        }

        public static Result CreateBcatSaveData(this FileSystemClient fs, TitleId applicationId, long size)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        TitleId = applicationId,
                        Type = SaveDataType.BcatDeliveryCacheStorage
                    };

                    var createInfo = new SaveDataCreateInfo
                    {
                        Size = size,
                        JournalSize = 0x200000,
                        BlockSize = 0x4000,
                        OwnerId = SystemTitleIds.Bcat,
                        Flags = 0,
                        SpaceId = SaveDataSpaceId.User
                    };

                    var metaInfo = new SaveMetaCreateInfo();

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref createInfo, ref metaInfo);
                },
                () => $", applicationid: 0x{applicationId.Value:X}, save_data_size: {size}");
        }

        public static Result CreateDeviceSaveData(this FileSystemClient fs, TitleId applicationId, TitleId ownerId,
            long size, long journalSize, uint flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        TitleId = applicationId,
                        Type = SaveDataType.DeviceSaveData
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

                    var metaInfo = new SaveMetaCreateInfo();

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref createInfo, ref metaInfo);
                },
                () => $", applicationid: 0x{applicationId.Value:X}, save_data_owner_id: 0x{ownerId.Value:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{flags:x8}");
        }

        public static Result CreateTemporaryStorage(this FileSystemClient fs, TitleId applicationId, TitleId ownerId, long size, uint flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        TitleId = applicationId,
                        Type = SaveDataType.TemporaryStorage
                    };

                    var createInfo = new SaveDataCreateInfo
                    {
                        Size = size,
                        BlockSize = 0x4000,
                        OwnerId = ownerId,
                        Flags = flags,
                        SpaceId = SaveDataSpaceId.TemporaryStorage
                    };

                    var metaInfo = new SaveMetaCreateInfo();

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref createInfo, ref metaInfo);
                },
                () => $", applicationid: 0x{applicationId.Value:X}, save_data_owner_id: 0x{ownerId.Value:X}, save_data_size: {size}, save_data_flags: 0x{flags:x8}");
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, UserId userId, TitleId ownerId, long size, long journalSize, uint flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
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
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();
                    return fsProxy.DeleteSaveDataFileSystem(saveDataId);
                },
                () => $", savedataid: 0x{saveDataId:X}");
        }

        public static Result DeleteSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();
                    return fsProxy.DeleteSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);
                },
                () => $", savedataspaceid: {spaceId}, savedataid: 0x{saveDataId:X}");
        }

        public static Result FindSaveDataWithFilter(this FileSystemClient fs, out SaveDataInfo info, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            info = default;

            SaveDataFilter tempFilter = filter;
            var tempInfo = new SaveDataInfo();

            Result result = fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    tempInfo = new SaveDataInfo();

                    Result rc = fsProxy.FindSaveDataWithFilter(out long count, SpanHelpers.AsByteSpan(ref tempInfo),
                        spaceId, ref tempFilter);
                    if (rc.IsFailure()) return rc;

                    if (count == 0)
                        return ResultFs.TargetNotFound.Log();

                    return Result.Success;
                },
                () => $", savedataspaceid: {spaceId}");

            if (result.IsSuccess())
            {
                info = tempInfo;
            }

            return result;
        }

        public static Result OpenSaveDataIterator(this FileSystemClient fs, out SaveDataIterator iterator, SaveDataSpaceId spaceId)
        {
            var tempIterator = new SaveDataIterator();

            Result result = fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    Result rc = fsProxy.OpenSaveDataInfoReaderBySaveDataSpaceId(out ISaveDataInfoReader reader, spaceId);
                    if (rc.IsFailure()) return rc;

                    tempIterator = new SaveDataIterator(fs, reader);

                    return Result.Success;
                },
                () => $", savedataspaceid: {spaceId}");

            iterator = result.IsSuccess() ? tempIterator : default;

            return result;
        }

        public static Result OpenSaveDataIterator(this FileSystemClient fs, out SaveDataIterator iterator, SaveDataSpaceId spaceId, ref SaveDataFilter filter)
        {
            var tempIterator = new SaveDataIterator();
            SaveDataFilter tempFilter = filter;

            Result result = fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    Result rc = fsProxy.OpenSaveDataInfoReaderWithFilter(out ISaveDataInfoReader reader, spaceId, ref tempFilter);
                    if (rc.IsFailure()) return rc;

                    tempIterator = new SaveDataIterator(fs, reader);

                    return Result.Success;
                },
                () => $", savedataspaceid: {spaceId}");

            iterator = result.IsSuccess() ? tempIterator : default;

            return result;
        }

        public static Result DisableAutoSaveDataCreation(this FileSystemClient fsClient)
        {
            IFileSystemProxy fsProxy = fsClient.GetFileSystemProxyServiceObject();

            return fsProxy.DisableAutoSaveDataCreation();
        }
    }

    public struct SaveDataIterator : IDisposable
    {
        private FileSystemClient FsClient { get; }
        private ISaveDataInfoReader Reader { get; }

        internal SaveDataIterator(FileSystemClient fsClient, ISaveDataInfoReader reader)
        {
            FsClient = fsClient;
            Reader = reader;
        }

        public Result ReadSaveDataInfo(out long readCount, Span<SaveDataInfo> buffer)
        {
            Result rc;

            Span<byte> byteBuffer = MemoryMarshal.Cast<SaveDataInfo, byte>(buffer);

            if (FsClient.IsEnabledAccessLog(AccessLogTarget.System))
            {
                TimeSpan startTime = FsClient.Time.GetCurrent();
                rc = Reader.ReadSaveDataInfo(out readCount, byteBuffer);
                TimeSpan endTime = FsClient.Time.GetCurrent();

                FsClient.OutputAccessLog(rc, startTime, endTime, $", size: {buffer.Length}");
            }
            else
            {
                rc = Reader.ReadSaveDataInfo(out readCount, byteBuffer);
            }

            return rc;
        }

        public void Dispose()
        {
            Reader?.Dispose();
        }
    }
}
