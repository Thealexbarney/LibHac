using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Sf;
using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs.Shim
{
    public readonly struct SaveDataIterator : IDisposable
    {
        private FileSystemClient FsClient { get; }
        private ReferenceCountedDisposable<ISaveDataInfoReader> Reader { get; }

        internal SaveDataIterator(FileSystemClient fsClient, ref ReferenceCountedDisposable<ISaveDataInfoReader> reader)
        {
            FsClient = fsClient;
            Reader = Shared.Move(ref reader);
        }

        private Result ReadSaveDataInfoImpl(out long readCount, Span<SaveDataInfo> buffer)
        {
            var outBuffer = new OutBuffer(MemoryMarshal.Cast<SaveDataInfo, byte>(buffer));
            return Reader.Target.Read(out readCount, outBuffer);
        }

        public Result ReadSaveDataInfo(out long readCount, Span<SaveDataInfo> buffer)
        {
            Result rc;
            FileSystemClient fs = FsClient;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = ReadSaveDataInfoImpl(out readCount, buffer);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSize).AppendFormat(buffer.Length);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = ReadSaveDataInfoImpl(out readCount, buffer);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public void Dispose()
        {
            Reader?.Dispose();
        }
    }

    [SkipLocalsInit]
    public static class SaveDataManagement
    {

        internal static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        internal static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        internal static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            throw new NotImplementedException();
        }

        internal static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, in SaveDataAttribute attribute,
            in SaveDataExtraData extraDataMask)
        {
            throw new NotImplementedException();
        }

        internal static Result WriteSaveDataFileSystemExtraData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId,
            ulong saveDataId, in SaveDataExtraData extraData)
        {
            throw new NotImplementedException();
        }

        internal static Result WriteSaveDataFileSystemExtraData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId,
            ulong saveDataId, in SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
        {
            throw new NotImplementedException();
        }

        internal static Result WriteSaveDataFileSystemExtraData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId,
            in SaveDataAttribute attribute, in SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
        {
            throw new NotImplementedException();
        }

        internal static Result FindSaveDataWithFilter(this FileSystemClientImpl fs, out SaveDataInfo saveInfo,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            saveInfo = default;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Unsafe.SkipInit(out SaveDataInfo tempInfo);
            OutBuffer saveInfoBuffer = OutBuffer.FromStruct(ref tempInfo);

            Result rc = fsProxy.Target.FindSaveDataWithFilter(out long count, saveInfoBuffer, spaceId, in filter);
            if (rc.IsFailure()) return rc;

            if (count == 0)
                return ResultFs.TargetNotFound.Log();

            saveInfo = tempInfo;
            return Result.Success;
        }

        public static Result CreateSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId, UserId userId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Account,
                userId, 0);
            if (rc.IsFailure()) return rc;

            rc = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                SaveDataSpaceId.User);
            if (rc.IsFailure()) return rc;

            var metaPolicy = new SaveDataMetaPolicy(SaveDataType.Account);
            metaPolicy.GenerateMetaInfo(out SaveDataMetaInfo metaInfo);

            return fsProxy.Target.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
        }

        public static Result CreateBcatSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId, long size)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Bcat,
                Fs.SaveData.InvalidUserId, 0);
            if (rc.IsFailure()) return rc;

            rc = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size,
                SaveDataProperties.BcatSaveDataJournalSize, SystemProgramId.Bcat.Value, SaveDataFlags.None,
                SaveDataSpaceId.User);
            if (rc.IsFailure()) return rc;

            var metaInfo = new SaveDataMetaInfo
            {
                Type = SaveDataMetaType.None,
                Size = 0
            };

            return fsProxy.Target.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
        }

        public static Result CreateDeviceSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Device,
                Fs.SaveData.InvalidUserId, 0);
            if (rc.IsFailure()) return rc;

            rc = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                SaveDataSpaceId.User);
            if (rc.IsFailure()) return rc;

            var metaPolicy = new SaveDataMetaPolicy(SaveDataType.Device);
            metaPolicy.GenerateMetaInfo(out SaveDataMetaInfo metaInfo);

            return fsProxy.Target.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
        }

        public static Result CreateCacheStorage(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, ushort index, long size, long journalSize, SaveDataFlags flags)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Cache,
                Fs.SaveData.InvalidUserId, 0, index);
            if (rc.IsFailure()) return rc;

            rc = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                spaceId);
            if (rc.IsFailure()) return rc;

            var metaInfo = new SaveDataMetaInfo
            {
                Type = SaveDataMetaType.None,
                Size = 0
            };

            return fsProxy.Target.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
        }

        public static Result CreateSaveData(this FileSystemClientImpl fs, in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in HashSalt hashSalt)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            return fsProxy.Target.CreateSaveDataFileSystemWithHashSalt(in attribute, in creationInfo,
                in metaInfo, in hashSalt);
        }

        public static Result DeleteSaveData(this FileSystemClientImpl fs, ulong saveDataId)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            return fsProxy.Target.DeleteSaveDataFileSystem(saveDataId);
        }

        public static Result DeleteSaveData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            return fsProxy.Target.DeleteSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);
        }

        public static Result DeleteSaveData(this FileSystemClient fs, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x30];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.DeleteSaveData(saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.DeleteSaveData(saveDataId);
            }
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result DeleteSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.DeleteSaveData(spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.DeleteSaveData(spaceId, saveDataId);
            }
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result OpenSaveDataIterator(this FileSystemClientImpl fs, out SaveDataIterator iterator,
            SaveDataSpaceId spaceId)
        {
            iterator = default;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<ISaveDataInfoReader> reader = null;
            try
            {
                Result rc = fsProxy.Target.OpenSaveDataInfoReaderBySaveDataSpaceId(out reader, spaceId);
                if (rc.IsFailure()) return rc;

                iterator = new SaveDataIterator(fs.Fs, ref reader);
            }
            finally
            {
                reader?.Dispose();
            }

            return Result.Success;
        }

        public static Result OpenSaveDataIterator(this FileSystemClientImpl fs, out SaveDataIterator iterator,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            iterator = default;

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            ReferenceCountedDisposable<ISaveDataInfoReader> reader = null;
            try
            {
                Result rc = fsProxy.Target.OpenSaveDataInfoReaderWithFilter(out reader, spaceId, in filter);
                if (rc.IsFailure()) return rc;

                iterator = new SaveDataIterator(fs.Fs, ref reader);
            }
            finally
            {
                reader?.Dispose();
            }

            return Result.Success;
        }

        public static Result OpenSaveDataIterator(this FileSystemClient fs, out SaveDataIterator iterator,
            SaveDataSpaceId spaceId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.OpenSaveDataIterator(out iterator, spaceId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId));

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.OpenSaveDataIterator(out iterator, spaceId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result OpenSaveDataIterator(this FileSystemClient fs, out SaveDataIterator iterator,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.OpenSaveDataIterator(out iterator, spaceId, in filter);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId));

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.OpenSaveDataIterator(out iterator, spaceId, in filter);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result FindSaveDataWithFilter(this FileSystemClient fs, out SaveDataInfo info,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.FindSaveDataWithFilter(out info, spaceId, in filter);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId));

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.FindSaveDataWithFilter(out info, spaceId, in filter);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result CreateSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.CreateSaveData(applicationId, userId, ownerId, size, journalSize, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16)
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size)
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize)
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.CreateSaveData(applicationId, userId, ownerId, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result CreateSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
            ulong ownerId, long size, long journalSize, in HashSalt hashSalt, SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = CreateSave(fs, applicationId, userId, ownerId, size, journalSize, in hashSalt, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16)
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size)
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize)
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = CreateSave(fs, applicationId, userId, ownerId, size, journalSize, in hashSalt, flags);
            }
            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result CreateSave(FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
                ulong ownerId, long size, long journalSize, in HashSalt hashSalt, SaveDataFlags flags)
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Account,
                    userId, 0);
                if (rc.IsFailure()) return rc;

                rc = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                    SaveDataSpaceId.User);
                if (rc.IsFailure()) return rc;

                var metaPolicy = new SaveDataMetaPolicy(SaveDataType.Account);
                metaPolicy.GenerateMetaInfo(out SaveDataMetaInfo metaInfo);

                return fsProxy.Target.CreateSaveDataFileSystemWithHashSalt(in attribute, in creationInfo, in metaInfo,
                    in hashSalt);
            }
        }

        public static Result CreateBcatSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, long size)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.CreateBcatSaveData(applicationId, size);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.CreateBcatSaveData(applicationId, size);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result CreateDeviceSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.CreateDeviceSaveData(applicationId, ownerId, size, journalSize, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size)
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize)
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.CreateDeviceSaveData(applicationId, ownerId, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result CreateTemporaryStorage(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, SaveDataFlags flags)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Temporary,
                Fs.SaveData.InvalidUserId, 0);
            if (rc.IsFailure()) return rc;

            rc = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, 0, ownerId, flags,
                SaveDataSpaceId.Temporary);
            if (rc.IsFailure()) return rc;

            var metaInfo = new SaveDataMetaInfo
            {
                Type = SaveDataMetaType.None,
                Size = 0
            };

            return fsProxy.Target.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
        }

        public static Result CreateTemporaryStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.CreateTemporaryStorage(applicationId, ownerId, size, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size)
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.CreateTemporaryStorage(applicationId, ownerId, size, flags);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, ushort index, long size, long journalSize, SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.CreateCacheStorage(applicationId, spaceId, ownerId, index, size, journalSize, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size)
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize)
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.CreateCacheStorage(applicationId, spaceId, ownerId, index, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateCacheStorage(fs, applicationId, spaceId, ownerId, 0, size, journalSize, flags);
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateCacheStorage(fs, applicationId, SaveDataSpaceId.User, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, UserId userId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = CreateSave(fs, spaceId, saveDataId, userId, ownerId, size, journalSize, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16)
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size)
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize)
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = CreateSave(fs, spaceId, saveDataId, userId, ownerId, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result CreateSave(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, UserId userId,
                ulong ownerId, long size, long journalSize, SaveDataFlags flags)
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, Fs.SaveData.InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (rc.IsFailure()) return rc;

                rc = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                    spaceId);
                if (rc.IsFailure()) return rc;

                return fsProxy.Target.CreateSaveDataFileSystemBySystemSaveDataId(in attribute, in creationInfo);
            }
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, ownerId, size, journalSize,
                flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, saveDataId, userId, 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, ulong ownerId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, saveDataId, Fs.SaveData.InvalidUserId, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, saveDataId, Fs.SaveData.InvalidUserId, 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, spaceId, saveDataId, Fs.SaveData.InvalidUserId, ownerId, size, journalSize,
                flags);
        }

        public static Result QuerySaveDataTotalSize(this FileSystemClientImpl fs, out long totalSize, long size,
            long journalSize)
        {
            Unsafe.SkipInit(out totalSize);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.QuerySaveDataTotalSize(out long tempTotalSize, size, journalSize);
            if (rc.IsSuccess())
                totalSize = tempTotalSize;

            return rc;
        }

        public static Result QuerySaveDataTotalSize(this FileSystemClient fs, out long totalSize, long size,
            long journalSize)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.QuerySaveDataTotalSize(out totalSize, size, journalSize);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSize).AppendFormat(size)
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.QuerySaveDataTotalSize(out totalSize, size, journalSize);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static void DisableAutoSaveDataCreation(this FileSystemClient fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.DisableAutoSaveDataCreation();

            fs.Impl.LogErrorMessage(rc);
            Abort.DoAbortUnless(rc.IsSuccess());
        }
    }
}
