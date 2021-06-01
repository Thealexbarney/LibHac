using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Sf;
using LibHac.Time;
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
                sb.Append(LogSize).AppendFormat(buffer.Length, 'd');

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
        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.ReadSaveDataFileSystemExtraData(OutBuffer.FromStruct(ref extraData), saveDataId);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(
                OutBuffer.FromStruct(ref extraData), spaceId, saveDataId);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.ReadSaveDataFileSystemExtraDataBySaveDataAttribute(
                OutBuffer.FromStruct(ref extraData), spaceId, in attribute);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, in SaveDataAttribute attribute,
            in SaveDataExtraData extraDataMask)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(
                OutBuffer.FromStruct(ref extraData), spaceId, in attribute, InBuffer.FromStruct(in extraDataMask));
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        /// <summary>
        /// Writes the <see cref="SaveDataExtraData.Flags"/> of the provided <see cref="SaveDataExtraData"/>
        /// to the save data in the specified <see cref="SaveDataSpaceId"/> with the provided save data ID.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="spaceId">The <see cref="SaveDataSpaceId"/> containing the save data to be written to.</param>
        /// <param name="saveDataId">The save data ID of the save data to be written to.</param>
        /// <param name="extraData">The <see cref="SaveDataExtraData"/> containing the data to write.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.TargetNotFound"/>: The save data was not found.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        public static Result WriteSaveDataFileSystemExtraData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId,
            ulong saveDataId, in SaveDataExtraData extraData)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.WriteSaveDataFileSystemExtraData(saveDataId, spaceId,
                InBuffer.FromStruct(in extraData));
            fs.AbortIfNeeded(rc);
            return rc;
        }

        /// <summary>
        /// Writes the provided <paramref name="extraData"/> to the save data in the specified
        /// <see cref="SaveDataSpaceId"/> with the provided save data ID.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="spaceId">The <see cref="SaveDataSpaceId"/> containing the save data to be written to.</param>
        /// <param name="saveDataId">The save data ID of the save data to be written to.</param>
        /// <param name="extraData">The <see cref="SaveDataExtraData"/> to write to the save data.</param>
        /// <param name="extraDataMask">A mask specifying which bits of <paramref name="extraData"/>
        /// to write to the save's extra data. 0 = don't write, 1 = write.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.TargetNotFound"/>: The save data was not found.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        /// <remarks>
        /// Calling programs may have permission to write to all, some or none of the save data's extra data.
        /// If any bits are set in <paramref name="extraDataMask"/> that the caller does not have the permissions
        /// to write to, nothing will be written and <see cref="ResultFs.PermissionDenied"/> will be returned.
        /// </remarks>
        public static Result WriteSaveDataFileSystemExtraData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId,
            ulong saveDataId, in SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.WriteSaveDataFileSystemExtraDataWithMask(saveDataId, spaceId,
                InBuffer.FromStruct(in extraData), InBuffer.FromStruct(in extraDataMask));
            fs.AbortIfNeeded(rc);
            return rc;
        }

        /// <summary>
        /// Writes the provided <paramref name="extraData"/> to the save data in the specified
        /// <see cref="SaveDataSpaceId"/> that matches the provided <see cref="SaveDataAttribute"/> key.
        /// The mask specifies which parts of the extra data will be written to the save data.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="spaceId">The <see cref="SaveDataSpaceId"/> containing the save data to be written to.</param>
        /// <param name="attribute">The key for the save data.</param>
        /// <param name="extraData">The <see cref="SaveDataExtraData"/> to write to the save data.</param>
        /// <param name="extraDataMask">A mask specifying which bits of <paramref name="extraData"/>
        /// to write to the save's extra data. 0 = don't write, 1 = write.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.TargetNotFound"/>: The save data was not found.<br/>
        /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
        /// <remarks>
        /// If <see cref="SaveDataAttribute.ProgramId"/> is set to <see cref="Fs.SaveData.AutoResolveCallerProgramId"/>,
        /// the program ID of <paramref name="attribute"/> will be resolved to the default save data program ID of the calling program.<br/>
        /// Calling programs may have permission to write to all, some or none of the save data's extra data.
        /// If any bits are set in <paramref name="extraDataMask"/> that the caller does not have the permissions
        /// to write to, nothing will be written and <see cref="ResultFs.PermissionDenied"/> will be returned.
        /// </remarks>
        public static Result WriteSaveDataFileSystemExtraData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId,
            in SaveDataAttribute attribute, in SaveDataExtraData extraData, in SaveDataExtraData extraDataMask)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in attribute,
                spaceId, InBuffer.FromStruct(in extraData), InBuffer.FromStruct(in extraDataMask));
            fs.AbortIfNeeded(rc);
            return rc;
        }

        public static Result FindSaveDataWithFilter(this FileSystemClientImpl fs, out SaveDataInfo saveInfo,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            UnsafeHelpers.SkipParamInit(out saveInfo);

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

        public static Result CreateSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            UserId userId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
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

        public static Result CreateBcatSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            long size)
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

        public static Result DeleteSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            UserId userId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Delete(fs, spaceId, saveDataId, userId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Delete(fs, spaceId, saveDataId, userId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result Delete(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, Fs.SaveData.InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (rc.IsFailure()) return rc;

                return fsProxy.Target.DeleteSaveDataFileSystemBySaveDataAttribute(spaceId, in attribute);
            }
        }

        public static Result DeleteDeviceSaveData(this FileSystemClient fs, ApplicationId applicationId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x30];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Delete(fs, applicationId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Delete(fs, applicationId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result Delete(FileSystemClient fs, ApplicationId applicationId)
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, new ProgramId(applicationId.Value),
                    SaveDataType.Device, Fs.SaveData.InvalidUserId, 0);
                if (rc.IsFailure()) return rc;

                return fsProxy.Target.DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId.User, in attribute);
            }
        }

        public static Result RegisterSaveDataAtomicDeletion(this FileSystemClient fs,
            ReadOnlySpan<ulong> saveDataIdList)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            var listBytes = new InBuffer(MemoryMarshal.Cast<ulong, byte>(saveDataIdList));

            Result rc = fsProxy.Target.RegisterSaveDataFileSystemAtomicDeletion(listBytes);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result OpenSaveDataIterator(this FileSystemClientImpl fs, out SaveDataIterator iterator,
            SaveDataSpaceId spaceId)
        {
            UnsafeHelpers.SkipParamInit(out iterator);

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
            UnsafeHelpers.SkipParamInit(out iterator);

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

        public static Result ReadSaveDataIteratorSaveDataInfo(out long readCount, Span<SaveDataInfo> buffer,
            in SaveDataIterator iterator)
        {
            return iterator.ReadSaveDataInfo(out readCount, buffer);
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
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
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
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
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
                    .Append(LogSaveDataSize).AppendFormat(size, 'd');

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
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
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
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
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
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
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
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
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

        public static Result ExtendSaveData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId, ulong saveDataId,
            long saveDataSize, long journalSize)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.Target.ExtendSaveDataFileSystem(spaceId, saveDataId, saveDataSize, journalSize);
        }

        public static Result ExtendSaveData(this FileSystemClient fs, ulong saveDataId, long saveDataSize,
            long journalSize)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x90];

            var spaceId = SaveDataSpaceId.System;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(saveDataSize, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result ExtendSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            long saveDataSize, long journalSize)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x90];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(saveDataSize, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result QuerySaveDataTotalSize(this FileSystemClientImpl fs, out long totalSize, long size,
            long journalSize)
        {
            UnsafeHelpers.SkipParamInit(out totalSize);

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
                sb.Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.QuerySaveDataTotalSize(out totalSize, size, journalSize);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetSaveDataOwnerId(this FileSystemClient fs, out ulong ownerId, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetOwnerId(fs, out ownerId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                // Note: Nintendo accidentally uses ", save_data_size: %ld" instead of ", savedataid: 0x%lX"
                // for the format string.
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetOwnerId(fs, out ownerId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetOwnerId(FileSystemClient fs, out ulong ownerId, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out ownerId);

                Result rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
                if (rc.IsFailure()) return rc;

                ownerId = extraData.OwnerId;
                return Result.Success;
            }
        }

        public static Result GetSaveDataOwnerId(this FileSystemClient fs, out ulong ownerId, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetOwnerId(fs, out ownerId, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetOwnerId(fs, out ownerId, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetOwnerId(FileSystemClient fs, out ulong ownerId, SaveDataSpaceId spaceId, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out ownerId);

                Result rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (rc.IsFailure()) return rc;

                ownerId = extraData.OwnerId;
                return Result.Success;
            }
        }

        public static Result GetSaveDataFlags(this FileSystemClient fs, out SaveDataFlags flags, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetFlags(fs, out flags, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetFlags(fs, out flags, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetFlags(FileSystemClient fs, out SaveDataFlags flags, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out flags);

                Result rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
                if (rc.IsFailure()) return rc;

                flags = extraData.Flags;
                return Result.Success;
            }
        }

        public static Result GetSaveDataFlags(this FileSystemClient fs, out SaveDataFlags flags,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetFlags(fs, out flags, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetFlags(fs, out flags, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetFlags(FileSystemClient fs, out SaveDataFlags flags, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out flags);

                Result rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (rc.IsFailure()) return rc;

                flags = extraData.Flags;
                return Result.Success;
            }
        }

        public static Result GetSystemSaveDataFlags(this FileSystemClient fs, out SaveDataFlags flags,
            SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetFlags(fs, out flags, spaceId, saveDataId, userId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetFlags(fs, out flags, spaceId, saveDataId, userId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetFlags(FileSystemClient fs, out SaveDataFlags flags, SaveDataSpaceId spaceId,
                ulong saveDataId, UserId userId)
            {
                UnsafeHelpers.SkipParamInit(out flags);

                Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, Fs.SaveData.InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (rc.IsFailure()) return rc;

                rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, in attribute);
                if (rc.IsFailure()) return rc;

                flags = extraData.Flags;
                return Result.Success;
            }
        }

        public static Result SetSaveDataFlags(this FileSystemClient fs, ulong saveDataId, SaveDataFlags flags)
        {
            return SetSaveDataFlags(fs, saveDataId, SaveDataSpaceId.System, flags);
        }

        public static Result SetSaveDataFlags(this FileSystemClient fs, ulong saveDataId, SaveDataSpaceId spaceId,
            SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x70];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = SetFlags(fs, saveDataId, spaceId, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = SetFlags(fs, saveDataId, spaceId, flags);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result SetFlags(FileSystemClient fs, ulong saveDataId, SaveDataSpaceId spaceId, SaveDataFlags flags)
            {
                Result rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (rc.IsFailure()) return rc;

                extraData.Flags = flags;

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData);
            }
        }

        public static Result SetSystemSaveDataFlags(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            UserId userId, SaveDataFlags flags)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0xA0];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = SetFlags(fs, spaceId, saveDataId, userId, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16)
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = SetFlags(fs, spaceId, saveDataId, userId, flags);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result SetFlags(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, UserId userId,
                SaveDataFlags flags)
            {
                var extraDataMask = new SaveDataExtraData();
                extraDataMask.Flags = unchecked((SaveDataFlags)0xFFFFFFFF);

                var extraData = new SaveDataExtraData();
                extraData.Flags = flags;

                Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, Fs.SaveData.InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (rc.IsFailure()) return rc;

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, in attribute, in extraData, in extraDataMask);
            }
        }

        public static Result GetSaveDataTimeStamp(this FileSystemClient fs, out PosixTime timeStamp, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x30];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetTimeStamp(fs, out timeStamp, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetTimeStamp(fs, out timeStamp, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetTimeStamp(FileSystemClient fs, out PosixTime timeStamp, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out timeStamp);

                Result rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
                if (rc.IsFailure()) return rc;

                timeStamp = new PosixTime(extraData.TimeStamp);
                return Result.Success;
            }
        }

        public static Result SetSaveDataTimeStamp(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            PosixTime timeStamp)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = SetTimeStamp(fs, spaceId, saveDataId, timeStamp);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataTimeStamp).AppendFormat(timeStamp.Value, 'd');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = SetTimeStamp(fs, spaceId, saveDataId, timeStamp);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result SetTimeStamp(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
                PosixTime timeStamp)
            {
                var extraDataMask = new SaveDataExtraData();
                extraDataMask.TimeStamp = unchecked((long)0xFFFFFFFFFFFFFFFF);

                var extraData = new SaveDataExtraData();
                extraData.TimeStamp = timeStamp.Value;

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in extraDataMask);
            }
        }

        public static Result GetSaveDataTimeStamp(this FileSystemClient fs, out PosixTime timeStamp,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetTimeStamp(fs, out timeStamp, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetTimeStamp(fs, out timeStamp, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetTimeStamp(FileSystemClient fs, out PosixTime timeStamp, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out timeStamp);

                Result rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (rc.IsFailure()) return rc;

                timeStamp = new PosixTime(extraData.TimeStamp);
                return Result.Success;
            }
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClientImpl fs, out long availableSize,
            ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out availableSize);

            Result rc = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
            if (rc.IsFailure()) return rc;

            availableSize = extraData.DataSize;
            return Result.Success;
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClientImpl fs, out long availableSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out availableSize);

            Result rc = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, saveDataId);
            if (rc.IsFailure()) return rc;

            availableSize = extraData.DataSize;
            return Result.Success;
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClient fs, out long availableSize,
            ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.GetSaveDataAvailableSize(out availableSize, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.GetSaveDataAvailableSize(out availableSize, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClient fs, out long availableSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.GetSaveDataAvailableSize(out availableSize, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.GetSaveDataAvailableSize(out availableSize, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClientImpl fs, out long journalSize,
            ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out journalSize);

            Result rc = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
            if (rc.IsFailure()) return rc;

            journalSize = extraData.JournalSize;
            return Result.Success;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClientImpl fs, out long journalSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out journalSize);

            Result rc = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, saveDataId);
            if (rc.IsFailure()) return rc;

            journalSize = extraData.JournalSize;
            return Result.Success;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClient fs, out long journalSize, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.GetSaveDataJournalSize(out journalSize, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.GetSaveDataJournalSize(out journalSize, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClient fs, out long journalSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.GetSaveDataJournalSize(out journalSize, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.GetSaveDataJournalSize(out journalSize, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetSaveDataCommitId(this FileSystemClient fs, out long commitId, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetCommitId(fs, out commitId, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetCommitId(fs, out commitId, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetCommitId(FileSystemClient fs, out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out commitId);

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fsProxy.Target.GetSaveDataCommitId(out commitId, spaceId, saveDataId);
            }
        }

        public static Result SetSaveDataCommitId(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            long commitId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = SetCommitId(fs, spaceId, saveDataId, commitId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataCommitId).AppendFormat(commitId, 'X', 16);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = SetCommitId(fs, spaceId, saveDataId, commitId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result SetCommitId(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, long commitId)
            {
                var extraDataMask = new SaveDataExtraData();
                extraDataMask.CommitId = unchecked((long)0xFFFFFFFFFFFFFFFF);

                var extraData = new SaveDataExtraData();
                extraData.CommitId = commitId;

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in extraDataMask);
            }
        }

        public static Result QuerySaveDataInternalStorageTotalSize(this FileSystemClientImpl fs, out long size,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out size);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();

            return fsProxy.Target.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
        }

        public static Result QuerySaveDataInternalStorageTotalSize(this FileSystemClient fs, out long size,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result VerifySaveData(this FileSystemClient fs, out bool isValid, ulong saveDataId,
            Span<byte> workBuffer)
        {
            return VerifySaveData(fs, out isValid, SaveDataSpaceId.System, saveDataId, workBuffer);
        }

        public static Result VerifySaveData(this FileSystemClient fs, out bool isValid, SaveDataSpaceId spaceId,
            ulong saveDataId, Span<byte> workBuffer)
        {
            UnsafeHelpers.SkipParamInit(out isValid);

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.VerifySaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId,
                new OutBuffer(workBuffer));

            if (ResultFs.DataCorrupted.Includes(rc))
            {
                isValid = false;
                return Result.Success;
            }

            fs.Impl.AbortIfNeeded(rc);

            if (rc.IsSuccess())
            {
                isValid = true;
                return Result.Success;
            }

            return rc;
        }

        public static Result CorruptSaveDataForDebug(this FileSystemClient fs, ulong saveDataId)
        {
            return CorruptSaveDataForDebug(fs, SaveDataSpaceId.System, saveDataId);
        }

        public static Result CorruptSaveDataForDebug(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.CorruptSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);

            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result CorruptSaveDataForDebug(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, long offset)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.CorruptSaveDataFileSystemByOffset(spaceId, saveDataId, offset);

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

        public static Result DeleteCacheStorage(this FileSystemClient fs, int index)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x20];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = Delete(fs, index);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogIndex).AppendFormat(index, 'd');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = Delete(fs, index);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result Delete(FileSystemClient fs, int index)
            {
                if (index < 0)
                    return ResultFs.InvalidArgument.Log();

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fsProxy.Target.DeleteCacheStorage((ushort)index);
            }
        }

        public static Result GetCacheStorageSize(this FileSystemClient fs, out long saveSize, out long journalSize,
            int index)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x60];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetSize(fs, out saveSize, out journalSize, index);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogIndex).AppendFormat(index, 'd')
                    .Append(LogSaveDataSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in saveSize, rc), 'd')
                    .Append(LogSaveDataJournalSize)
                    .AppendFormat(AccessLogImpl.DereferenceOutValue(in journalSize, rc), 'd');

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetSize(fs, out saveSize, out journalSize, index);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetSize(FileSystemClient fs, out long saveSize, out long journalSize, int index)
            {
                UnsafeHelpers.SkipParamInit(out saveSize, out journalSize);

                if (index < 0)
                    return ResultFs.InvalidArgument.Log();

                // Note: Nintendo gets the service object in the outer function and captures it for the inner function
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fsProxy.Target.GetCacheStorageSize(out saveSize, out journalSize, (ushort)index);
            }
        }

        public static Result UpdateSaveDataMacForDebug(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return fsProxy.Target.UpdateSaveDataMacForDebug(spaceId, saveDataId);
        }

        public static Result ListApplicationAccessibleSaveDataOwnerId(this FileSystemClient fs, out int readCount,
            Span<Ncm.ApplicationId> idBuffer, Ncm.ApplicationId applicationId, int programIndex, int startIndex)
        {
            if (idBuffer.IsEmpty)
            {
                readCount = 0;
                return Result.Success;
            }

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            var programId = new ProgramId(applicationId.Value + (uint)programIndex);
            var idOutBuffer = new OutBuffer(MemoryMarshal.Cast<Ncm.ApplicationId, byte>(idBuffer));

            Result rc = fsProxy.Target.ListAccessibleSaveDataOwnerId(out readCount, idOutBuffer, programId, startIndex,
                idBuffer.Length);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result GetSaveDataRestoreFlag(this FileSystemClient fs, out bool isRestoreFlagSet,
            U8Span mountName)
        {
            UnsafeHelpers.SkipParamInit(out isRestoreFlagSet);

            Result rc;
            FileSystemAccessor fileSystem;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog())
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = fs.Impl.Find(out fileSystem, mountName);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(mountName).Append((byte)'"');

                fs.Impl.OutputAccessLogUnlessResultSuccess(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = fs.Impl.Find(out fileSystem, mountName);
            }

            fs.Impl.AbortIfNeeded(rc);
            if (rc.IsFailure()) return rc;

            if (fs.Impl.IsEnabledAccessLog() && fileSystem.IsEnabledAccessLog())
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetRestoreFlagValue(fs, out isRestoreFlagSet, fileSystem);
                Tick end = fs.Hos.Os.GetSystemTick();

                ReadOnlySpan<byte> isSetString =
                    AccessLogImpl.ConvertFromBoolToAccessLogBooleanValue(
                        AccessLogImpl.DereferenceOutValue(in isRestoreFlagSet, rc));

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(mountName).Append((byte)'"')
                    .Append(LogRestoreFlag).Append(isSetString);

                fs.Impl.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetRestoreFlagValue(fs, out isRestoreFlagSet, fileSystem);
            }

            fs.Impl.AbortIfNeeded(rc);
            return rc;

            static Result GetRestoreFlagValue(FileSystemClient fs, out bool isRestoreFlagSet,
                FileSystemAccessor fileSystem)
            {
                Unsafe.SkipInit(out isRestoreFlagSet);

                if (fileSystem is null)
                    return ResultFs.NullptrArgument.Log();

                Result rc = fileSystem.GetSaveDataAttribute(out SaveDataAttribute attribute);
                if (rc.IsFailure()) return rc;

                if (attribute.ProgramId == Fs.SaveData.InvalidProgramId)
                    attribute.ProgramId = Fs.SaveData.AutoResolveCallerProgramId;

                var extraDataMask = new SaveDataExtraData();
                extraDataMask.Flags = SaveDataFlags.Restore;

                rc = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, SaveDataSpaceId.User,
                    in attribute, in extraDataMask);
                if (rc.IsFailure()) return rc;

                isRestoreFlagSet = extraData.Flags.HasFlag(SaveDataFlags.Restore);
                return Result.Success;
            }
        }

        public static Result GetDeviceSaveDataSize(this FileSystemClientImpl fs, out long saveSize,
            out long journalSize, ApplicationId applicationId)
        {
            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x70];

            if (fs.IsEnabledAccessLog() && fs.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = GetSize(fs, out saveSize, out journalSize, applicationId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in saveSize, rc), 'd')
                    .Append(LogSaveDataJournalSize)
                    .AppendFormat(AccessLogImpl.DereferenceOutValue(in journalSize, rc), 'd');

                fs.OutputAccessLog(rc, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                rc = GetSize(fs, out saveSize, out journalSize, applicationId);
            }

            fs.AbortIfNeeded(rc);
            return rc;

            static Result GetSize(FileSystemClientImpl fs, out long saveSize, out long journalSize,
                ApplicationId applicationId)
            {
                UnsafeHelpers.SkipParamInit(out saveSize, out journalSize);

                var extraDataMask = new SaveDataExtraData();
                extraDataMask.DataSize = unchecked((long)0xFFFFFFFFFFFFFFFF);
                extraDataMask.JournalSize = unchecked((long)0xFFFFFFFFFFFFFFFF);

                Result rc = SaveDataAttribute.Make(out SaveDataAttribute attribute, new ProgramId(applicationId.Value),
                    SaveDataType.Device, Fs.SaveData.InvalidUserId, 0);
                if (rc.IsFailure()) return rc;

                rc = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, SaveDataSpaceId.User,
                    in attribute, in extraDataMask);
                if (rc.IsFailure()) return rc;

                saveSize = extraData.DataSize;
                journalSize = extraData.JournalSize;

                return Result.Success;
            }
        }
    }
}