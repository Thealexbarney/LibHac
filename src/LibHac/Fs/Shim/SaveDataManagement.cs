using System;
using System.Collections.Generic;
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
using static LibHac.Fs.SaveData;

namespace LibHac.Fs
{
    /// <summary>
    /// Allows iterating through the <see cref="SaveDataInfo"/> of a list of save data.
    /// </summary>
    /// <remarks>Based on nnSdk 14.3.0</remarks>
    public class SaveDataIterator : IDisposable
    {
        private readonly FileSystemClient _fsClient;
        private SharedRef<ISaveDataInfoReader> _reader;

        internal SaveDataIterator(FileSystemClient fsClient, ref SharedRef<ISaveDataInfoReader> reader)
        {
            _reader = SharedRef<ISaveDataInfoReader>.CreateMove(ref reader);
            _fsClient = fsClient;
        }

        public void Dispose()
        {
            _reader.Destroy();
        }

        private Result ReadSaveDataInfoImpl(out long readCount, Span<SaveDataInfo> buffer)
        {
            Result res = _reader.Get.Read(out readCount, OutBuffer.FromSpan(buffer));
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public Result ReadSaveDataInfo(out long readCount, Span<SaveDataInfo> buffer)
        {
            Result res;
            FileSystemClient fs = _fsClient;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = ReadSaveDataInfoImpl(out readCount, buffer);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSize).AppendFormat(buffer.Length, 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = ReadSaveDataInfoImpl(out readCount, buffer);
            }

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
    }
}

namespace LibHac.Fs.Shim
{
    /// <summary>
    /// Contains functions for creating, deleting, and otherwise managing save data.
    /// </summary>
    /// <remarks>Based on nnSdk 14.3.0</remarks>
    [SkipLocalsInit]
    public static class SaveDataManagement
    {
        private const int SaveDataBlockSize = 0x4000;

        private class CacheStorageListCache : IDisposable
        {
            public readonly struct CacheEntry
            {
                private readonly int _index;

                public CacheEntry(int index) => _index = index;
                public int GetCacheStorageIndex() => _index;
            }

            private int _position;
            private List<CacheEntry> _entryList;

            public CacheStorageListCache()
            {
                _position = 0;
                _entryList = new List<CacheEntry>();
            }

            public void Dispose() { }

            public Result PushBack(in CacheEntry entry)
            {
                _entryList.Add(entry);

                // The original code can have allocation failures here
                return Result.Success;
            }

            public ref readonly CacheEntry PopFront()
            {
                if (_position >= _entryList.Count)
                    return ref Unsafe.NullRef<CacheEntry>();

                return ref CollectionsMarshal.AsSpan(_entryList)[_position++];
            }

            public static CacheStorageListCache GetCacheStorageListCache(CacheStorageListHandle handle)
            {
                return (CacheStorageListCache)handle.Cache;
            }
        }

        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.ReadSaveDataFileSystemExtraData(OutBuffer.FromStruct(ref extraData), saveDataId);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(
                OutBuffer.FromStruct(ref extraData), spaceId, saveDataId);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.ReadSaveDataFileSystemExtraDataBySaveDataAttribute(
                OutBuffer.FromStruct(ref extraData), spaceId, in attribute);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result ReadSaveDataFileSystemExtraData(this FileSystemClientImpl fs,
            out SaveDataExtraData extraData, SaveDataSpaceId spaceId, in SaveDataAttribute attribute,
            in SaveDataExtraData extraDataMask)
        {
            UnsafeHelpers.SkipParamInit(out extraData);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(
                OutBuffer.FromStruct(ref extraData), spaceId, in attribute, InBuffer.FromStruct(in extraDataMask));
            if (res.IsFailure()) return res.Miss();

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
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.WriteSaveDataFileSystemExtraData(saveDataId, spaceId,
                InBuffer.FromStruct(in extraData));
            fs.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
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
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.WriteSaveDataFileSystemExtraDataWithMask(saveDataId, spaceId,
                InBuffer.FromStruct(in extraData), InBuffer.FromStruct(in extraDataMask));
            fs.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
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
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in attribute,
                spaceId, InBuffer.FromStruct(in extraData), InBuffer.FromStruct(in extraDataMask));
            fs.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result FindSaveDataWithFilter(this FileSystemClientImpl fs, out SaveDataInfo saveInfo,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            UnsafeHelpers.SkipParamInit(out saveInfo);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Unsafe.SkipInit(out SaveDataInfo tempInfo);
            OutBuffer saveInfoBuffer = OutBuffer.FromStruct(ref tempInfo);

            Result res = fileSystemProxy.Get.FindSaveDataWithFilter(out long count, saveInfoBuffer, spaceId, in filter);
            if (res.IsFailure()) return res.Miss();

            if (count == 0)
                return ResultFs.TargetNotFound.Log();

            saveInfo = tempInfo;
            return Result.Success;
        }

        public static Result CreateSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            UserId userId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Account,
                userId, InvalidSystemSaveDataId);
            if (res.IsFailure()) return res.Miss();

            res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                SaveDataSpaceId.User);
            if (res.IsFailure()) return res.Miss();

            var metaPolicy = new SaveDataMetaPolicy(SaveDataType.Account);
            metaPolicy.GenerateMetaInfo(out SaveDataMetaInfo metaInfo);

            res = fileSystemProxy.Get.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result CreateBcatSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            long size)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Bcat,
                InvalidUserId, InvalidSystemSaveDataId);
            if (res.IsFailure()) return res.Miss();

            res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size,
                SaveDataProperties.BcatSaveDataJournalSize, SystemProgramId.Bcat.Value, SaveDataFlags.None,
                SaveDataSpaceId.User);
            if (res.IsFailure()) return res.Miss();

            var metaInfo = new SaveDataMetaInfo
            {
                Type = SaveDataMetaType.None,
                Size = 0
            };

            res = fileSystemProxy.Get.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result CreateDeviceSaveData(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Device,
                InvalidUserId, InvalidSystemSaveDataId);
            if (res.IsFailure()) return res.Miss();

            res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                SaveDataSpaceId.User);
            if (res.IsFailure()) return res.Miss();

            var metaPolicy = new SaveDataMetaPolicy(SaveDataType.Device);
            metaPolicy.GenerateMetaInfo(out SaveDataMetaInfo metaInfo);

            res = fileSystemProxy.Get.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result CreateCacheStorage(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, ushort index, long size, long journalSize, SaveDataFlags flags)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Cache,
                InvalidUserId, InvalidSystemSaveDataId, index);
            if (res.IsFailure()) return res.Miss();

            res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                spaceId);
            if (res.IsFailure()) return res.Miss();

            var metaInfo = new SaveDataMetaInfo
            {
                Type = SaveDataMetaType.None,
                Size = 0
            };

            res = fileSystemProxy.Get.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result CreateSaveData(this FileSystemClientImpl fs, in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in HashSalt hashSalt)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            return fileSystemProxy.Get.CreateSaveDataFileSystemWithHashSalt(in attribute, in creationInfo,
                in metaInfo, in hashSalt);
        }

        public static Result DeleteSaveData(this FileSystemClientImpl fs, ulong saveDataId)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            return fileSystemProxy.Get.DeleteSaveDataFileSystem(saveDataId);
        }

        public static Result DeleteSaveData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            return fileSystemProxy.Get.DeleteSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);
        }

        public static Result DeleteSaveData(this FileSystemClient fs, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x30];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.DeleteSaveData(saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.DeleteSaveData(saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result DeleteSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.DeleteSaveData(spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.DeleteSaveData(spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result DeleteSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            UserId userId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = Delete(fs, spaceId, saveDataId, userId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = Delete(fs, spaceId, saveDataId, userId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result Delete(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
            {
                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (res.IsFailure()) return res.Miss();

                return fileSystemProxy.Get.DeleteSaveDataFileSystemBySaveDataAttribute(spaceId, in attribute);
            }
        }

        public static Result DeleteDeviceSaveData(this FileSystemClient fs, ApplicationId applicationId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x30];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = Delete(fs, applicationId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = Delete(fs, applicationId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result Delete(FileSystemClient fs, ApplicationId applicationId)
            {
                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, new ProgramId(applicationId.Value),
                    SaveDataType.Device, InvalidUserId, InvalidSystemSaveDataId);
                if (res.IsFailure()) return res.Miss();

                return fileSystemProxy.Get.DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId.User, in attribute);
            }
        }

        public static Result RegisterSaveDataAtomicDeletion(this FileSystemClient fs,
            ReadOnlySpan<ulong> saveDataIdList)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.RegisterSaveDataFileSystemAtomicDeletion(InBuffer.FromSpan(saveDataIdList));
            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result OpenSaveDataIterator(this FileSystemClientImpl fs,
            ref UniqueRef<SaveDataIterator> outIterator, SaveDataSpaceId spaceId)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            using var reader = new SharedRef<ISaveDataInfoReader>();

            Result res = fileSystemProxy.Get.OpenSaveDataInfoReaderBySaveDataSpaceId(ref reader.Ref(), spaceId);
            if (res.IsFailure()) return res.Miss();

            using var iterator = new UniqueRef<SaveDataIterator>(new SaveDataIterator(fs.Fs, ref reader.Ref()));

            if (!iterator.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataManagementA.Log();

            outIterator.Set(ref iterator.Ref());

            return Result.Success;
        }

        public static Result OpenSaveDataIterator(this FileSystemClientImpl fs,
            ref UniqueRef<SaveDataIterator> outIterator, SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            using var reader = new SharedRef<ISaveDataInfoReader>();

            Result res = fileSystemProxy.Get.OpenSaveDataInfoReaderWithFilter(ref reader.Ref(), spaceId, in filter);
            if (res.IsFailure()) return res.Miss();

            using var iterator = new UniqueRef<SaveDataIterator>(new SaveDataIterator(fs.Fs, ref reader.Ref()));

            if (!iterator.HasValue)
                return ResultFs.AllocationMemoryFailedInSaveDataManagementA.Log();

            outIterator.Set(ref iterator.Ref());

            return Result.Success;
        }

        public static Result ReadSaveDataIteratorSaveDataInfo(this FileSystemClientImpl fs, out long readCount,
            Span<SaveDataInfo> buffer, SaveDataIterator iterator)
        {
            Result res = iterator.ReadSaveDataInfo(out readCount, buffer);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        public static Result OpenSaveDataIterator(this FileSystemClient fs, ref UniqueRef<SaveDataIterator> outIterator,
            SaveDataSpaceId spaceId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.OpenSaveDataIterator(ref outIterator, spaceId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId));

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.OpenSaveDataIterator(ref outIterator, spaceId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result OpenSaveDataIterator(this FileSystemClient fs, ref UniqueRef<SaveDataIterator> outIterator,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.OpenSaveDataIterator(ref outIterator, spaceId, in filter);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId));

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.OpenSaveDataIterator(ref outIterator, spaceId, in filter);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result FindSaveDataWithFilter(this FileSystemClient fs, out SaveDataInfo info,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.FindSaveDataWithFilter(out info, spaceId, in filter);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId));

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.FindSaveDataWithFilter(out info, spaceId, in filter);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result CreateSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.CreateSaveData(applicationId, userId, ownerId, size, journalSize, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16)
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.CreateSaveData(applicationId, userId, ownerId, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result CreateSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
            ulong ownerId, long size, long journalSize, in HashSalt hashSalt, SaveDataFlags flags)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = CreateSave(fs, applicationId, userId, ownerId, size, journalSize, in hashSalt, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16)
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = CreateSave(fs, applicationId, userId, ownerId, size, journalSize, in hashSalt, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result CreateSave(FileSystemClient fs, Ncm.ApplicationId applicationId, UserId userId,
                ulong ownerId, long size, long journalSize, in HashSalt hashSalt, SaveDataFlags flags)
            {
                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Account,
                    userId, InvalidSystemSaveDataId);
                if (res.IsFailure()) return res.Miss();

                res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                    SaveDataSpaceId.User);
                if (res.IsFailure()) return res.Miss();

                var metaPolicy = new SaveDataMetaPolicy(SaveDataType.Account);
                metaPolicy.GenerateMetaInfo(out SaveDataMetaInfo metaInfo);

                return fileSystemProxy.Get.CreateSaveDataFileSystemWithHashSalt(in attribute, in creationInfo, in metaInfo,
                    in hashSalt);
            }
        }

        public static Result CreateBcatSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId, long size)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.CreateBcatSaveData(applicationId, size);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size, 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.CreateBcatSaveData(applicationId, size);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result CreateDeviceSaveData(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.CreateDeviceSaveData(applicationId, ownerId, size, journalSize, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.CreateDeviceSaveData(applicationId, ownerId, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result CreateTemporaryStorage(this FileSystemClientImpl fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, SaveDataFlags flags)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, applicationId, SaveDataType.Temporary,
                InvalidUserId, InvalidSystemSaveDataId);
            if (res.IsFailure()) return res.Miss();

            res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize: 0, ownerId, flags,
                SaveDataSpaceId.Temporary);
            if (res.IsFailure()) return res.Miss();

            var metaInfo = new SaveDataMetaInfo
            {
                Type = SaveDataMetaType.None,
                Size = 0
            };

            return fileSystemProxy.Get.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
        }

        public static Result CreateTemporaryStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, SaveDataFlags flags)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.CreateTemporaryStorage(applicationId, ownerId, size, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.CreateTemporaryStorage(applicationId, ownerId, size, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, ushort index, long size, long journalSize, SaveDataFlags flags)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.CreateCacheStorage(applicationId, spaceId, ownerId, index, size, journalSize, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.CreateCacheStorage(applicationId, spaceId, ownerId, index, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            SaveDataSpaceId spaceId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateCacheStorage(fs, applicationId, spaceId, ownerId, index: 0, size, journalSize, flags);
        }

        public static Result CreateCacheStorage(this FileSystemClient fs, Ncm.ApplicationId applicationId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateCacheStorage(fs, applicationId, SaveDataSpaceId.User, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, UserId userId, ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x100];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = CreateSave(fs, spaceId, saveDataId, userId, ownerId, size, journalSize, flags);
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

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = CreateSave(fs, spaceId, saveDataId, userId, ownerId, size, journalSize, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result CreateSave(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, UserId userId,
                ulong ownerId, long size, long journalSize, SaveDataFlags flags)
            {
                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (res.IsFailure()) return res.Miss();

                res = SaveDataCreationInfo.Make(out SaveDataCreationInfo creationInfo, size, journalSize, ownerId, flags,
                    spaceId);
                if (res.IsFailure()) return res.Miss();

                return fileSystemProxy.Get.CreateSaveDataFileSystemBySystemSaveDataId(in attribute, in creationInfo);
            }
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, SaveDataSpaceId.System, saveDataId, userId, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, UserId userId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, saveDataId, userId, ownerId: 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, ulong ownerId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, saveDataId, InvalidUserId, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, ulong saveDataId, long size,
            long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, saveDataId, InvalidUserId, ownerId: 0, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags)
        {
            return CreateSystemSaveData(fs, spaceId, saveDataId, InvalidUserId, ownerId, size, journalSize, flags);
        }

        public static Result CreateSystemSaveData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId,
            ulong saveDataId, UserId userId, ulong ownerId, long size, long journalSize, SaveDataFlags flags,
            SaveDataFormatType formatType)
        {
            if (formatType == SaveDataFormatType.NoJournal && journalSize != 0)
                return ResultFs.InvalidArgument.Log();

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId, SaveDataType.System,
                userId, saveDataId);
            if (res.IsFailure()) return res.Miss();

            res = SaveDataCreationInfo2.Make(out SaveDataCreationInfo2 creationInfo, in attribute, size, journalSize,
                SaveDataBlockSize, ownerId, flags, spaceId, formatType);
            if (res.IsFailure()) return res.Miss();

            creationInfo.MetaType = SaveDataMetaType.None;
            creationInfo.MetaSize = 0;

            return fileSystemProxy.Get.CreateSaveDataFileSystemWithCreationInfo2(in creationInfo).Ret();
        }

        public static Result CreateSystemSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            ulong ownerId, long size, long journalSize, SaveDataFlags flags, SaveDataFormatType formatType)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x180];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = CreateSystemSaveData(fs.Impl, spaceId, saveDataId, InvalidUserId, ownerId, size, journalSize,
                    flags, formatType);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(InvalidUserId.Id.High, 'X', 16).AppendFormat(InvalidUserId.Id.Low, 'X', 16)
                    .Append(LogSaveDataOwnerId).AppendFormat(ownerId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(size, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd')
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8)
                    .Append(LogSaveDataFormatType).Append(idString.ToString(formatType));

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = CreateSystemSaveData(fs.Impl, spaceId, saveDataId, InvalidUserId, ownerId, size, journalSize,
                    flags, formatType);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result ExtendSaveData(this FileSystemClientImpl fs, SaveDataSpaceId spaceId, ulong saveDataId,
            long saveDataSize, long journalSize)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            return fileSystemProxy.Get.ExtendSaveDataFileSystem(spaceId, saveDataId, saveDataSize, journalSize);
        }

        public static Result ExtendSaveData(this FileSystemClient fs, ulong saveDataId, long saveDataSize,
            long journalSize)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x90];

            var spaceId = SaveDataSpaceId.System;

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(saveDataSize, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result ExtendSaveData(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            long saveDataSize, long journalSize)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x90];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSize).AppendFormat(saveDataSize, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(journalSize, 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.ExtendSaveData(spaceId, saveDataId, saveDataSize, journalSize);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result QuerySaveDataTotalSize(this FileSystemClientImpl fs, out long totalSize, long saveDataSize,
            long saveDataJournalSize)
        {
            UnsafeHelpers.SkipParamInit(out totalSize);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.QuerySaveDataTotalSize(out long tempTotalSize, saveDataSize,
                saveDataJournalSize);
            if (res.IsFailure()) return res.Miss();

            totalSize = tempTotalSize;
            return Result.Success;
        }

        public static Result QuerySaveDataTotalSize(this FileSystemClient fs, out long totalSize, long saveDataSize,
            long saveDataJournalSize)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.QuerySaveDataTotalSize(out totalSize, saveDataSize, saveDataJournalSize);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataSize).AppendFormat(saveDataSize, 'd')
                    .Append(LogSaveDataJournalSize).AppendFormat(saveDataJournalSize, 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.QuerySaveDataTotalSize(out totalSize, saveDataSize, saveDataJournalSize);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result GetSaveDataOwnerId(this FileSystemClient fs, out ulong ownerId, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetOwnerId(fs, out ownerId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                // Note: Nintendo accidentally uses ", save_data_size: %ld" instead of ", savedataid: 0x%lX"
                // for the format string.
                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetOwnerId(fs, out ownerId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetOwnerId(FileSystemClient fs, out ulong ownerId, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out ownerId);

                Result res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
                if (res.IsFailure()) return res.Miss();

                ownerId = extraData.OwnerId;
                return Result.Success;
            }
        }

        public static Result GetSaveDataOwnerId(this FileSystemClient fs, out ulong ownerId, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetOwnerId(fs, out ownerId, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetOwnerId(fs, out ownerId, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetOwnerId(FileSystemClient fs, out ulong ownerId, SaveDataSpaceId spaceId, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out ownerId);

                Result res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (res.IsFailure()) return res.Miss();

                ownerId = extraData.OwnerId;
                return Result.Success;
            }
        }

        public static Result GetSaveDataFlags(this FileSystemClient fs, out SaveDataFlags flags, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetFlags(fs, out flags, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetFlags(fs, out flags, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetFlags(FileSystemClient fs, out SaveDataFlags flags, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out flags);

                Result res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
                if (res.IsFailure()) return res.Miss();

                flags = extraData.Flags;
                return Result.Success;
            }
        }

        public static Result GetSaveDataFlags(this FileSystemClient fs, out SaveDataFlags flags,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetFlags(fs, out flags, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetFlags(fs, out flags, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetFlags(FileSystemClient fs, out SaveDataFlags flags, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out flags);

                Result res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (res.IsFailure()) return res.Miss();

                flags = extraData.Flags;
                return Result.Success;
            }
        }

        public static Result GetSystemSaveDataFlags(this FileSystemClient fs, out SaveDataFlags flags,
            SaveDataSpaceId spaceId, ulong saveDataId, UserId userId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetFlags(fs, out flags, spaceId, saveDataId, userId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetFlags(fs, out flags, spaceId, saveDataId, userId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetFlags(FileSystemClient fs, out SaveDataFlags flags, SaveDataSpaceId spaceId,
                ulong saveDataId, UserId userId)
            {
                UnsafeHelpers.SkipParamInit(out flags);

                Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (res.IsFailure()) return res.Miss();

                res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, in attribute);
                if (res.IsFailure()) return res.Miss();

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
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x70];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = SetFlags(fs, saveDataId, spaceId, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = SetFlags(fs, saveDataId, spaceId, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result SetFlags(FileSystemClient fs, ulong saveDataId, SaveDataSpaceId spaceId, SaveDataFlags flags)
            {
                Result res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (res.IsFailure()) return res.Miss();

                extraData.Flags = flags;

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData);
            }
        }

        public static Result SetSystemSaveDataFlags(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            UserId userId, SaveDataFlags flags)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0xA0];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = SetFlags(fs, spaceId, saveDataId, userId, flags);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogUserId).AppendFormat(userId.Id.High, 'X', 16).AppendFormat(userId.Id.Low, 'X', 16)
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataFlags).AppendFormat((int)flags, 'X', 8);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = SetFlags(fs, spaceId, saveDataId, userId, flags);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result SetFlags(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, UserId userId,
                SaveDataFlags flags)
            {
                SaveDataExtraData extraDataMask = default;
                extraDataMask.Flags = unchecked((SaveDataFlags)0xFFFFFFFF);

                SaveDataExtraData extraData = default;
                extraData.Flags = flags;

                Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, InvalidProgramId,
                    SaveDataType.System, userId, saveDataId);
                if (res.IsFailure()) return res.Miss();

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, in attribute, in extraData, in extraDataMask);
            }
        }

        public static Result GetSaveDataTimeStamp(this FileSystemClient fs, out PosixTime timeStamp, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x30];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetTimeStamp(fs, out timeStamp, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetTimeStamp(fs, out timeStamp, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetTimeStamp(FileSystemClient fs, out PosixTime timeStamp, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out timeStamp);

                Result res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
                if (res.IsFailure()) return res.Miss();

                timeStamp = new PosixTime(extraData.TimeStamp);
                return Result.Success;
            }
        }

        public static Result SetSaveDataTimeStamp(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            PosixTime timeStamp)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = SetTimeStamp(fs, spaceId, saveDataId, timeStamp);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataTimeStamp).AppendFormat(timeStamp.Value, 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = SetTimeStamp(fs, spaceId, saveDataId, timeStamp);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result SetTimeStamp(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
                PosixTime timeStamp)
            {
                SaveDataExtraData extraDataMask = default;
                extraDataMask.TimeStamp = unchecked((long)0xFFFFFFFFFFFFFFFF);

                SaveDataExtraData extraData = default;
                extraData.TimeStamp = timeStamp.Value;

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in extraDataMask);
            }
        }

        public static Result GetSaveDataTimeStamp(this FileSystemClient fs, out PosixTime timeStamp,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetTimeStamp(fs, out timeStamp, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetTimeStamp(fs, out timeStamp, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetTimeStamp(FileSystemClient fs, out PosixTime timeStamp, SaveDataSpaceId spaceId,
                ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out timeStamp);

                Result res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId,
                    saveDataId);
                if (res.IsFailure()) return res.Miss();

                timeStamp = new PosixTime(extraData.TimeStamp);
                return Result.Success;
            }
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClientImpl fs, out long availableSize,
            ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out availableSize);

            Result res = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
            if (res.IsFailure()) return res.Miss();

            availableSize = extraData.DataSize;
            return Result.Success;
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClientImpl fs, out long availableSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out availableSize);

            Result res = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, saveDataId);
            if (res.IsFailure()) return res.Miss();

            availableSize = extraData.DataSize;
            return Result.Success;
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClient fs, out long availableSize,
            ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.GetSaveDataAvailableSize(out availableSize, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.GetSaveDataAvailableSize(out availableSize, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result GetSaveDataAvailableSize(this FileSystemClient fs, out long availableSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.GetSaveDataAvailableSize(out availableSize, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.GetSaveDataAvailableSize(out availableSize, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClientImpl fs, out long journalSize,
            ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out journalSize);

            Result res = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, saveDataId);
            if (res.IsFailure()) return res.Miss();

            journalSize = extraData.JournalSize;
            return Result.Success;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClientImpl fs, out long journalSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out journalSize);

            Result res = fs.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, spaceId, saveDataId);
            if (res.IsFailure()) return res.Miss();

            journalSize = extraData.JournalSize;
            return Result.Success;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClient fs, out long journalSize, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.GetSaveDataJournalSize(out journalSize, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.GetSaveDataJournalSize(out journalSize, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result GetSaveDataJournalSize(this FileSystemClient fs, out long journalSize,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.GetSaveDataJournalSize(out journalSize, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.GetSaveDataJournalSize(out journalSize, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result GetSaveDataCommitId(this FileSystemClient fs, out long commitId, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetCommitId(fs, out commitId, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetCommitId(fs, out commitId, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetCommitId(FileSystemClient fs, out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
            {
                UnsafeHelpers.SkipParamInit(out commitId);

                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fileSystemProxy.Get.GetSaveDataCommitId(out commitId, spaceId, saveDataId);
            }
        }

        public static Result SetSaveDataCommitId(this FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId,
            long commitId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x80];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = SetCommitId(fs, spaceId, saveDataId, commitId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataId).AppendFormat(saveDataId, 'X')
                    .Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataCommitId).AppendFormat(commitId, 'X', 16);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = SetCommitId(fs, spaceId, saveDataId, commitId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result SetCommitId(FileSystemClient fs, SaveDataSpaceId spaceId, ulong saveDataId, long commitId)
            {
                SaveDataExtraData extraDataMask = default;
                extraDataMask.CommitId = unchecked((long)0xFFFFFFFFFFFFFFFF);

                SaveDataExtraData extraData = default;
                extraData.CommitId = commitId;

                return fs.Impl.WriteSaveDataFileSystemExtraData(spaceId, saveDataId, in extraData, in extraDataMask);
            }
        }

        public static Result QuerySaveDataInternalStorageTotalSize(this FileSystemClientImpl fs, out long size,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out size);

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();

            return fileSystemProxy.Get.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
        }

        public static Result QuerySaveDataInternalStorageTotalSize(this FileSystemClient fs, out long size,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x50];

            if (fs.Impl.IsEnabledAccessLog(AccessLogTarget.System) && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var idString = new IdString();
                var sb = new U8StringBuilder(logBuffer, true);

                sb.Append(LogSaveDataSpaceId).Append(idString.ToString(spaceId))
                    .Append(LogSaveDataId).AppendFormat(saveDataId, 'X');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;
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

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.VerifySaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId,
                new OutBuffer(workBuffer));

            if (ResultFs.DataCorrupted.Includes(res))
            {
                isValid = false;
                return Result.Success;
            }

            fs.Impl.AbortIfNeeded(res);

            if (res.IsSuccess())
            {
                isValid = true;
                return Result.Success;
            }

            return res;
        }

        public static Result CorruptSaveDataForDebug(this FileSystemClient fs, ulong saveDataId)
        {
            return CorruptSaveDataForDebug(fs, SaveDataSpaceId.System, saveDataId);
        }

        public static Result CorruptSaveDataForDebug(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.CorruptSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result CorruptSaveDataForDebug(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId, long offset)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.CorruptSaveDataFileSystemByOffset(spaceId, saveDataId, offset);

            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static void DisableAutoSaveDataCreation(this FileSystemClient fs)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.DisableAutoSaveDataCreation();

            fs.Impl.LogResultErrorMessage(res);
            Abort.DoAbortUnless(res.IsSuccess());
        }

        public static Result DeleteCacheStorage(this FileSystemClient fs, int index)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x20];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = Delete(fs, index);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogIndex).AppendFormat(index, 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = Delete(fs, index);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result Delete(FileSystemClient fs, int index)
            {
                if (index < 0)
                    return ResultFs.InvalidArgument.Log();

                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fileSystemProxy.Get.DeleteCacheStorage((ushort)index);
            }
        }

        public static Result GetCacheStorageSize(this FileSystemClient fs, out long saveSize, out long journalSize,
            int index)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x60];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetSize(fs, out saveSize, out journalSize, index);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogIndex).AppendFormat(index, 'd')
                    .Append(LogSaveDataSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in saveSize, res), 'd')
                    .Append(LogSaveDataJournalSize)
                    .AppendFormat(AccessLogImpl.DereferenceOutValue(in journalSize, res), 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetSize(fs, out saveSize, out journalSize, index);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetSize(FileSystemClient fs, out long saveSize, out long journalSize, int index)
            {
                UnsafeHelpers.SkipParamInit(out saveSize, out journalSize);

                if (index < 0)
                    return ResultFs.InvalidArgument.Log();

                // Note: Nintendo gets the service object in the outer function and captures it for the inner function
                using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fileSystemProxy.Get.GetCacheStorageSize(out saveSize, out journalSize, (ushort)index);
            }
        }

        public static Result OpenCacheStorageList(this FileSystemClient fs, out CacheStorageListHandle handle)
        {
            UnsafeHelpers.SkipParamInit(out handle);

            Result res;
            Span<byte> logBuffer = stackalloc byte[0x40];

            CacheStorageListCache listCache;

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = Open(fs, out listCache);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogCacheStorageListHandle).AppendFormat(listCache.GetHashCode(), 'x');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = Open(fs, out listCache);
            }

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            handle = new CacheStorageListHandle(listCache);
            return Result.Success;

            static Result Open(FileSystemClient fs, out CacheStorageListCache listCache)
            {
                UnsafeHelpers.SkipParamInit(out listCache);
                CacheStorageListCache tempListCache = null;

                Result res = Utility.DoContinuouslyUntilSaveDataListFetched(fs.Hos, () =>
                {
                    // Note: Nintendo uses the same CacheStorageListCache for every attempt to fetch the save data list
                    // without clearing it between runs. This means that if it has to retry fetching the list, the
                    // CacheStorageListCache may contain duplicate entries if the save data indexer was reset while this
                    // function was running.
                    tempListCache = new CacheStorageListCache();

                    using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
                    using var reader = new SharedRef<ISaveDataInfoReader>();

                    Result result = fileSystemProxy.Get.OpenSaveDataInfoReaderOnlyCacheStorage(ref reader.Ref());
                    if (result.IsFailure()) return result.Miss();

                    while (true)
                    {
                        Unsafe.SkipInit(out SaveDataInfo info);

                        result = reader.Get.Read(out long readCount, new OutBuffer(SpanHelpers.AsByteSpan(ref info)));
                        if (result.IsFailure()) return result.Miss();

                        if (readCount == 0)
                            break;

                        var cacheEntry = new CacheStorageListCache.CacheEntry(info.Index);
                        result = tempListCache.PushBack(in cacheEntry);
                        if (result.IsFailure()) return result.Miss();
                    }

                    return Result.Success;
                });
                if (res.IsFailure()) return res.Miss();

                Assert.SdkRequiresNotNull(tempListCache);

                listCache = tempListCache;
                return Result.Success;
            }
        }

        public static Result ReadCacheStorageList(this FileSystemClient fs, out int storageInfoReadCount,
            Span<CacheStorageInfo> storageInfoBuffer, CacheStorageListHandle handle)
        {
            UnsafeHelpers.SkipParamInit(out storageInfoReadCount);

            Result res;
            Span<byte> logBuffer = stackalloc byte[0x70];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = Read(out storageInfoReadCount, storageInfoBuffer, handle);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogCacheStorageListHandle).AppendFormat(handle.Cache.GetHashCode(), 'x')
                    .Append(LogInfoBufferCount).AppendFormat(storageInfoBuffer.Length, 'X')
                    .Append(LogCacheStorageCount).AppendFormat(AccessLogImpl.DereferenceOutValue(in storageInfoReadCount, res), 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = Read(out storageInfoReadCount, storageInfoBuffer, handle);
            }

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;

            static Result Read(out int storageCount, Span<CacheStorageInfo> infoBuffer, CacheStorageListHandle handle)
            {
                int count = 0;

                var listCache = CacheStorageListCache.GetCacheStorageListCache(handle);

                while (count < infoBuffer.Length)
                {
                    ref readonly CacheStorageListCache.CacheEntry entry = ref listCache.PopFront();

                    // We're done iterating if we get a null ref
                    if (Unsafe.IsNullRef(ref Unsafe.AsRef(in entry)))
                        break;

                    infoBuffer[count] = default;
                    infoBuffer[count].Index = entry.GetCacheStorageIndex();
                    count++;
                }

                storageCount = count;
                return Result.Success;
            }
        }

        public static void CloseCacheStorageList(this FileSystemClient fs, CacheStorageListHandle handle)
        {
            Span<byte> logBuffer = stackalloc byte[0x40];

            var listCache = CacheStorageListCache.GetCacheStorageListCache(handle);

            listCache?.Dispose();

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogCacheStorageListHandle).AppendFormat(handle.Cache.GetHashCode(), 'x');

                fs.Impl.OutputAccessLog(Result.Success, start, end, null, new U8Span(sb.Buffer));
            }
        }

        public static Result UpdateSaveDataMacForDebug(this FileSystemClient fs, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.UpdateSaveDataMacForDebug(spaceId, saveDataId);
            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result ListApplicationAccessibleSaveDataOwnerId(this FileSystemClient fs, out int readCount,
            Span<Ncm.ApplicationId> idBuffer, Ncm.ApplicationId applicationId, int programIndex, int startIndex)
        {
            if (idBuffer.IsEmpty)
            {
                readCount = 0;
                return Result.Success;
            }

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            var programId = new ProgramId(applicationId.Value + (uint)programIndex);
            OutBuffer idOutBuffer = OutBuffer.FromSpan(idBuffer);

            Result res = fileSystemProxy.Get.ListAccessibleSaveDataOwnerId(out readCount, idOutBuffer, programId, startIndex,
                idBuffer.Length);
            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result GetSaveDataRestoreFlag(this FileSystemClient fs, out bool isRestoreFlagSet,
            U8Span mountName)
        {
            UnsafeHelpers.SkipParamInit(out isRestoreFlagSet);

            Result res;
            FileSystemAccessor fileSystem;
            Span<byte> logBuffer = stackalloc byte[0x40];

            if (fs.Impl.IsEnabledAccessLog())
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = fs.Impl.Find(out fileSystem, mountName);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(mountName).Append(LogQuote);

                fs.Impl.OutputAccessLogUnlessResultSuccess(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = fs.Impl.Find(out fileSystem, mountName);
            }

            fs.Impl.AbortIfNeeded(res);
            if (res.IsFailure()) return res.Miss();

            if (fs.Impl.IsEnabledAccessLog() && fileSystem.IsEnabledAccessLog())
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetRestoreFlagValue(fs, out isRestoreFlagSet, fileSystem);
                Tick end = fs.Hos.Os.GetSystemTick();

                ReadOnlySpan<byte> isSetString =
                    AccessLogImpl.ConvertFromBoolToAccessLogBooleanValue(
                        AccessLogImpl.DereferenceOutValue(in isRestoreFlagSet, res));

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogName).Append(mountName).Append(LogQuote)
                    .Append(LogRestoreFlag).Append(isSetString);

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetRestoreFlagValue(fs, out isRestoreFlagSet, fileSystem);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetRestoreFlagValue(FileSystemClient fs, out bool isRestoreFlagSet,
                FileSystemAccessor fileSystem)
            {
                Unsafe.SkipInit(out isRestoreFlagSet);

                if (fileSystem is null)
                    return ResultFs.NullptrArgument.Log();

                Result res = fileSystem.GetSaveDataAttribute(out SaveDataAttribute attribute);
                if (res.IsFailure()) return res.Miss();

                if (attribute.ProgramId == InvalidProgramId)
                    attribute.ProgramId = AutoResolveCallerProgramId;

                SaveDataExtraData extraDataMask = default;
                extraDataMask.Flags = SaveDataFlags.Restore;

                res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, SaveDataSpaceId.User,
                    in attribute, in extraDataMask);
                if (res.IsFailure()) return res.Miss();

                isRestoreFlagSet = extraData.Flags.HasFlag(SaveDataFlags.Restore);
                return Result.Success;
            }
        }

        public static Result GetDeviceSaveDataSize(this FileSystemClient fs, out long saveSize,
            out long journalSize, ApplicationId applicationId)
        {
            Result res;
            Span<byte> logBuffer = stackalloc byte[0x70];

            if (fs.Impl.IsEnabledAccessLog() && fs.Impl.IsEnabledHandleAccessLog(null))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                res = GetSize(fs, out saveSize, out journalSize, applicationId);
                Tick end = fs.Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogApplicationId).AppendFormat(applicationId.Value, 'X')
                    .Append(LogSaveDataSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in saveSize, res), 'd')
                    .Append(LogSaveDataJournalSize)
                    .AppendFormat(AccessLogImpl.DereferenceOutValue(in journalSize, res), 'd');

                fs.Impl.OutputAccessLog(res, start, end, null, new U8Span(sb.Buffer));
            }
            else
            {
                res = GetSize(fs, out saveSize, out journalSize, applicationId);
            }

            fs.Impl.AbortIfNeeded(res);
            return res;

            static Result GetSize(FileSystemClient fs, out long saveSize, out long journalSize,
                ApplicationId applicationId)
            {
                UnsafeHelpers.SkipParamInit(out saveSize, out journalSize);

                SaveDataExtraData extraDataMask = default;
                extraDataMask.DataSize = unchecked((long)0xFFFFFFFFFFFFFFFF);
                extraDataMask.JournalSize = unchecked((long)0xFFFFFFFFFFFFFFFF);

                Result res = SaveDataAttribute.Make(out SaveDataAttribute attribute, new ProgramId(applicationId.Value),
                    SaveDataType.Device, InvalidUserId, InvalidSystemSaveDataId);
                if (res.IsFailure()) return res.Miss();

                res = fs.Impl.ReadSaveDataFileSystemExtraData(out SaveDataExtraData extraData, SaveDataSpaceId.User,
                    in attribute, in extraDataMask);
                if (res.IsFailure()) return res.Miss();

                saveSize = extraData.DataSize;
                journalSize = extraData.JournalSize;

                return Result.Success;
            }
        }
    }
}