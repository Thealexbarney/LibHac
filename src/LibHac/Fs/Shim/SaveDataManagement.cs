using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.FsService;
using LibHac.Ncm;

namespace LibHac.Fs.Shim
{
    public static class SaveDataManagement
    {
        public static Result CreateSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        ProgramId = applicationId,
                        UserId = userId,
                        Type = SaveDataType.Account
                    };

                    var createInfo = new SaveDataCreationInfo
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
                        Type = SaveDataMetaType.Thumbnail,
                        Size = 0x40060
                    };

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref createInfo, ref metaInfo);
                },
                () =>
                    $", applicationid: 0x{applicationId.Value:X}, userid: 0x{userId}, save_data_owner_id: 0x{ownerId:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{(int)flags:X8}");
        }

        public static Result CreateSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
            ulong ownerId, long size, long journalSize, HashSalt hashSalt, SaveDataFlags flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        ProgramId = applicationId,
                        UserId = userId,
                        Type = SaveDataType.Account
                    };

                    var createInfo = new SaveDataCreationInfo
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
                        Type = SaveDataMetaType.Thumbnail,
                        Size = 0x40060
                    };

                    return fsProxy.CreateSaveDataFileSystemWithHashSalt(ref attribute, ref createInfo, ref metaInfo,
                        ref hashSalt);
                },
                () =>
                    $", applicationid: 0x{applicationId.Value:X}, userid: 0x{userId}, save_data_owner_id: 0x{ownerId:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{(int)flags:X8}");
        }

        public static Result CreateBcatSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, long size)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        ProgramId = applicationId,
                        Type = SaveDataType.Bcat
                    };

                    var createInfo = new SaveDataCreationInfo
                    {
                        Size = size,
                        JournalSize = 0x200000,
                        BlockSize = 0x4000,
                        OwnerId = SystemProgramId.Bcat.Value,
                        Flags = 0,
                        SpaceId = SaveDataSpaceId.User
                    };

                    var metaInfo = new SaveMetaCreateInfo();

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref createInfo, ref metaInfo);
                },
                () => $", applicationid: 0x{applicationId.Value:X}, save_data_size: {size}");
        }

        public static Result CreateDeviceSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, ulong ownerId,
            long size, long journalSize, SaveDataFlags flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        ProgramId = applicationId,
                        Type = SaveDataType.Device
                    };

                    var createInfo = new SaveDataCreationInfo
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
                () => $", applicationid: 0x{applicationId.Value:X}, save_data_owner_id: 0x{ownerId:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{(int)flags:X8}");
        }

        public static Result CreateTemporaryStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId, ulong ownerId, long size, SaveDataFlags flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        ProgramId = applicationId,
                        Type = SaveDataType.Temporary
                    };

                    var createInfo = new SaveDataCreationInfo
                    {
                        Size = size,
                        BlockSize = 0x4000,
                        OwnerId = ownerId,
                        Flags = flags,
                        SpaceId = SaveDataSpaceId.Temporary
                    };

                    var metaInfo = new SaveMetaCreateInfo();

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref createInfo, ref metaInfo);
                },
                () => $", applicationid: 0x{applicationId.Value:X}, save_data_owner_id: 0x{ownerId:X}, save_data_size: {size}, save_data_flags: 0x{(int)flags:X8}");
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, short index, long size, long journalSize, SaveDataFlags flags)
        {
            return fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    var attribute = new SaveDataAttribute
                    {
                        ProgramId = applicationId,
                        Type = SaveDataType.Cache,
                        Index = index
                    };

                    var creationInfo = new SaveDataCreationInfo
                    {
                        Size = size,
                        JournalSize = journalSize,
                        BlockSize = 0x4000,
                        OwnerId = ownerId,
                        Flags = flags,
                        SpaceId = spaceId
                    };

                    var metaInfo = new SaveMetaCreateInfo();

                    return fsProxy.CreateSaveDataFileSystem(ref attribute, ref creationInfo, ref metaInfo);
                },
                () => $", applicationid: 0x{applicationId.Value:X}, savedataspaceid: {spaceId}, save_data_owner_id: 0x{ownerId:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{(int)flags:X8}");
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateCacheStorage(fs, applicationId, spaceId, ownerId, 0, size, journalSize, flags);
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateCacheStorage(fs, applicationId, SaveDataSpaceId.User, ownerId, 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, UserId userId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
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

                    var createInfo = new SaveDataCreationInfo
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
                () => $", savedataspaceid: {spaceId}, savedataid: 0x{saveDataId:X}, userid: 0x{userId.Id.High:X16}{userId.Id.Low:X16}, save_data_owner_id: 0x{ownerId:X}, save_data_size: {size}, save_data_journal_size: {journalSize}, save_data_flags: 0x{(int)flags:x8}");
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, ulong ownerId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, UserId.Zero, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, UserId.Zero, 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
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

        public static Result QuerySaveDataTotalSize(this FileSystemClient fs, out long totalSize, long size, long journalSize)
        {
            totalSize = default;

            long totalSizeTemp = 0;

            Result result = fs.RunOperationWithAccessLog(AccessLogTarget.System,
                () =>
                {
                    IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

                    return fsProxy.QuerySaveDataTotalSize(out totalSizeTemp, size, journalSize);
                },
                () => $", save_data_size: {size}, save_data_journal_size: {journalSize}");

            if (result.IsSuccess())
            {
                totalSize = totalSizeTemp;
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
