using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using LibHac.Kvdb;
using LibHac.Ncm;
using LibHac.Spl;

namespace LibHac.FsService
{
    public class FileSystemProxy : IFileSystemProxy
    {
        private FileSystemProxyCore FsProxyCore { get; }
        private FileSystemServer FsServer { get; }

        public long CurrentProcess { get; private set; }

        public long SaveDataSize { get; private set; }
        public long SaveDataJournalSize { get; private set; }
        public FsPath SaveDataRootPath { get; } = default;
        public bool AutoCreateSaveData { get; private set; }

        internal FileSystemProxy(FileSystemProxyCore fsProxyCore, FileSystemServer fsServer)
        {
            FsProxyCore = fsProxyCore;
            FsServer = fsServer;

            CurrentProcess = -1;
            SaveDataSize = 0x2000000;
            SaveDataJournalSize = 0x1000000;
            AutoCreateSaveData = true;
        }

        public Result OpenFileSystemWithId(out IFileSystem fileSystem, ref FsPath path, TitleId titleId, FileSystemProxyType type)
        {
            fileSystem = default;

            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            bool canMountSystemDataPrivate = false;

            Result rc = PathTools.Normalize(out U8Span normalizedPath, path);
            if (rc.IsFailure()) return rc;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            return FsProxyCore.OpenFileSystem(out fileSystem, normalizedPath, type, canMountSystemDataPrivate, titleId);
        }

        public Result OpenFileSystemWithPatch(out IFileSystem fileSystem, TitleId titleId, FileSystemProxyType type)
        {
            throw new NotImplementedException();
        }

        public Result SetCurrentProcess(long processId)
        {
            CurrentProcess = processId;

            return Result.Success;
        }

        public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByCurrentProcess(out IFileSystem fileSystem)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByProgramId(out IFileSystem fileSystem, TitleId titleId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByCurrentProcess(out IStorage storage)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByProgramId(out IStorage storage, TitleId programId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageByDataId(out IStorage storage, TitleId dataId, StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenPatchDataStorageByCurrentProcess(out IStorage storage)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemWithProgramIndex(out IFileSystem fileSystem, byte programIndex)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataStorageWithProgramIndex(out IStorage storage, byte programIndex)
        {
            throw new NotImplementedException();
        }

        public Result RegisterSaveDataFileSystemAtomicDeletion(ReadOnlySpan<ulong> saveDataIds)
        {
            throw new NotImplementedException();
        }

        public Result DeleteSaveDataFileSystem(ulong saveDataId)
        {
            return DeleteSaveDataFileSystemImpl(SaveDataSpaceId.System, saveDataId);
        }

        private Result DeleteSaveDataFileSystemImpl(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            SaveDataIndexerReader reader = default;

            try
            {
                Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out reader, spaceId);
                if (rc.IsFailure()) return rc;

                if (saveDataId == FileSystemServer.SaveIndexerId)
                {
                    // missing: This save can only be deleted by the FS process itself
                }
                else
                {
                    if (spaceId != SaveDataSpaceId.ProperSystem && spaceId != SaveDataSpaceId.SafeMode)
                    {
                        rc = reader.Indexer.GetBySaveDataId(out SaveDataIndexerValue value, saveDataId);
                        if (rc.IsFailure()) return rc;

                        spaceId = value.SpaceId;
                    }

                    rc = reader.Indexer.GetKey(out SaveDataAttribute key, saveDataId);
                    if (rc.IsFailure()) return rc;

                    if (key.Type == SaveDataType.System || key.Type == SaveDataType.SystemBcat)
                    {
                        // Check if permissions allow deleting system save data
                    }
                    else
                    {
                        // Check if permissions allow deleting save data
                    }

                    reader.Indexer.SetState(saveDataId, SaveDataState.Creating);
                    if (rc.IsFailure()) return rc;

                    reader.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                rc = DeleteSaveDataFileSystemImpl2(spaceId, saveDataId);
                if (rc.IsFailure()) return rc;

                if (saveDataId != FileSystemServer.SaveIndexerId)
                {
                    rc = reader.Indexer.Delete(saveDataId);
                    if (rc.IsFailure()) return rc;

                    rc = reader.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }
            finally
            {
                reader.Dispose();
            }
        }

        private Result DeleteSaveDataFileSystemImpl2(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            // missing: Check extra data flags for this value. Bit 3
            bool doSecureDelete = false;

            Result rc = FsProxyCore.DeleteSaveDataMetaFiles(saveDataId, spaceId);
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                return rc;

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            rc = FsProxyCore.DeleteSaveDataFileSystem(spaceId, saveDataId, doSecureDelete);
            if (rc.IsFailure() && !ResultFs.PathNotFound.Includes(rc))
                return rc;

            return Result.Success;
        }

        public Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            return DeleteSaveDataFileSystemBySaveDataSpaceIdImpl(spaceId, saveDataId);
        }

        private Result DeleteSaveDataFileSystemBySaveDataSpaceIdImpl(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            if (saveDataId != FileSystemServer.SaveIndexerId)
            {
                SaveDataIndexerReader reader = default;

                try
                {
                    Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out reader, spaceId);
                    if (rc.IsFailure()) return rc;

                    rc = reader.Indexer.GetBySaveDataId(out SaveDataIndexerValue value, saveDataId);
                    if (rc.IsFailure()) return rc;

                    if (value.SpaceId != GetSpaceIdForIndexer(spaceId))
                        return ResultFs.TargetNotFound.Log();
                }
                finally
                {
                    reader.Dispose();
                }
            }

            return DeleteSaveDataFileSystemImpl(spaceId, saveDataId);
        }

        private Result GetSaveDataInfo(out SaveDataInfo info, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute)
        {
            info = default;

            SaveDataIndexerReader reader = default;

            try
            {
                Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out reader, spaceId);
                if (rc.IsFailure()) return rc;

                rc = reader.Indexer.Get(out SaveDataIndexerValue value, ref attribute);
                if (rc.IsFailure()) return rc;

                SaveDataIndexer.GetSaveDataInfo(out info, ref attribute, ref value);
                return Result.Success;
            }
            finally
            {
                reader.Dispose();
            }
        }

        public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, ref SaveDataAttribute attribute)
        {
            Result rs = GetSaveDataInfo(out SaveDataInfo info, spaceId, ref attribute);
            if (rs.IsFailure()) return rs;

            return DeleteSaveDataFileSystemBySaveDataSpaceIdImpl(spaceId, info.SaveDataId);
        }

        public Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        private Result CreateSaveDataFileSystemImpl(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo, ref OptionalHashSalt hashSalt, bool something)
        {
            ulong saveDataId = 0;
            bool isDeleteNeeded = false;
            Result rc;

            SaveDataIndexerReader reader = default;

            try
            {
                if (attribute.SaveDataId == FileSystemServer.SaveIndexerId)
                {
                    saveDataId = FileSystemServer.SaveIndexerId;
                    rc = FsProxyCore.DoesSaveDataExist(out bool saveExists, creationInfo.SpaceId, saveDataId);

                    if (rc.IsSuccess() && saveExists)
                    {
                        return ResultFs.PathAlreadyExists.Log();
                    }

                    isDeleteNeeded = true;
                }
                else
                {
                    rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out reader, creationInfo.SpaceId);
                    if (rc.IsFailure()) return rc;

                    SaveDataAttribute indexerKey = attribute;

                    if (attribute.SaveDataId != 0 && attribute.UserId == UserId.Zero)
                    {
                        saveDataId = attribute.SaveDataId;

                        rc = reader.Indexer.AddSystemSaveData(ref indexerKey);
                    }
                    else
                    {
                        if (attribute.Type != SaveDataType.System &&
                            attribute.Type != SaveDataType.SystemBcat)
                        {
                            if (reader.Indexer.IsFull())
                            {
                                return ResultKvdb.TooLargeKeyOrDbFull.Log();
                            }
                        }

                        rc = reader.Indexer.Add(out saveDataId, ref indexerKey);
                    }

                    if (ResultFs.SaveDataPathAlreadyExists.Includes(rc))
                    {
                        return ResultFs.PathAlreadyExists.LogConverted(rc);
                    }

                    isDeleteNeeded = true;

                    rc = reader.Indexer.SetState(saveDataId, SaveDataState.Creating);
                    if (rc.IsFailure()) return rc;

                    SaveDataSpaceId indexerSpaceId = GetSpaceIdForIndexer(creationInfo.SpaceId);

                    rc = reader.Indexer.SetSpaceId(saveDataId, indexerSpaceId);
                    if (rc.IsFailure()) return rc;

                    // todo: calculate size
                    long size = 0;

                    rc = reader.Indexer.SetSize(saveDataId, size);
                    if (rc.IsFailure()) return rc;

                    rc = reader.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                rc = FsProxyCore.CreateSaveDataFileSystem(saveDataId, ref attribute, ref creationInfo, SaveDataRootPath,
                    hashSalt, false);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathAlreadyExists.Includes(rc)) return rc;

                    rc = DeleteSaveDataFileSystemImpl2(creationInfo.SpaceId, saveDataId);
                    if (rc.IsFailure()) return rc;

                    rc = FsProxyCore.CreateSaveDataFileSystem(saveDataId, ref attribute, ref creationInfo, SaveDataRootPath,
                        hashSalt, false);
                    if (rc.IsFailure()) return rc;
                }

                if (metaCreateInfo.Type != SaveDataMetaType.None)
                {
                    rc = FsProxyCore.CreateSaveDataMetaFile(saveDataId, creationInfo.SpaceId, metaCreateInfo.Type,
                        metaCreateInfo.Size);
                    if (rc.IsFailure()) return rc;

                    if (metaCreateInfo.Type == SaveDataMetaType.Thumbnail)
                    {
                        rc = FsProxyCore.OpenSaveDataMetaFile(out IFile metaFile, saveDataId, creationInfo.SpaceId,
                            metaCreateInfo.Type);

                        using (metaFile)
                        {
                            if (rc.IsFailure()) return rc;

                            ReadOnlySpan<byte> metaFileData = stackalloc byte[0x20];

                            rc = metaFile.Write(0, metaFileData, WriteOption.Flush);
                            if (rc.IsFailure()) return rc;
                        }
                    }
                }

                if (attribute.SaveDataId == FileSystemServer.SaveIndexerId || something)
                {
                    isDeleteNeeded = false;

                    return Result.Success;
                }

                rc = reader.Indexer.SetState(saveDataId, SaveDataState.Normal);
                if (rc.IsFailure()) return rc;

                rc = reader.Indexer.Commit();
                if (rc.IsFailure()) return rc;

                isDeleteNeeded = false;

                return Result.Success;
            }
            finally
            {
                // Revert changes if an error happened in the middle of creation
                if (isDeleteNeeded)
                {
                    DeleteSaveDataFileSystemImpl2(creationInfo.SpaceId, saveDataId);

                    if (reader.IsInitialized && saveDataId != FileSystemServer.SaveIndexerId)
                    {
                        rc = reader.Indexer.GetBySaveDataId(out SaveDataIndexerValue value, saveDataId);

                        if (rc.IsSuccess() && value.SpaceId == creationInfo.SpaceId)
                        {
                            reader.Indexer.Delete(saveDataId);
                            reader.Indexer.Commit();
                        }
                    }
                }

                reader.Dispose();
            }
        }

        public Result CreateSaveDataFileSystem(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo)
        {
            OptionalHashSalt hashSalt = default;

            return CreateUserSaveDataFileSystem(ref attribute, ref creationInfo, ref metaCreateInfo, ref hashSalt);
        }

        public Result CreateSaveDataFileSystemWithHashSalt(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo, ref HashSalt hashSalt)
        {
            var hashSaltCopy = new OptionalHashSalt
            {
                IsSet = true,
                HashSalt = hashSalt
            };

            return CreateUserSaveDataFileSystem(ref attribute, ref creationInfo, ref metaCreateInfo, ref hashSaltCopy);
        }

        private Result CreateUserSaveDataFileSystem(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo,
            ref SaveMetaCreateInfo metaCreateInfo, ref OptionalHashSalt hashSalt)
        {
            return CreateSaveDataFileSystemImpl(ref attribute, ref creationInfo, ref metaCreateInfo, ref hashSalt, false);
        }

        public Result CreateSaveDataFileSystemBySystemSaveDataId(ref SaveDataAttribute attribute, ref SaveDataCreationInfo creationInfo)
        {
            if (!IsSystemSaveDataId(attribute.SaveDataId))
                return ResultFs.InvalidArgument.Log();

            SaveDataCreationInfo newCreationInfo = creationInfo;

            if (creationInfo.OwnerId == TitleId.Zero)
            {
                // todo: Assign the current program's ID
                // throw new NotImplementedException();
            }

            // Missing permission checks

            if (attribute.Type == SaveDataType.SystemBcat)
            {
                newCreationInfo.OwnerId = SystemTitleIds.Bcat;
            }

            SaveMetaCreateInfo metaCreateInfo = default;
            OptionalHashSalt optionalHashSalt = default;

            return CreateSaveDataFileSystemImpl(ref attribute, ref newCreationInfo, ref metaCreateInfo,
                ref optionalHashSalt, false);
        }

        public Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize, long journalSize)
        {
            throw new NotImplementedException();
        }

        private Result OpenSaveDataFileSystemImpl(out IFileSystem fileSystem, out ulong saveDataId,
            SaveDataSpaceId spaceId, ref SaveDataAttribute attribute, bool openReadOnly, bool cacheExtraData)
        {
            fileSystem = default;
            saveDataId = default;

            bool hasFixedId = attribute.SaveDataId != 0 && attribute.UserId == UserId.Zero;

            if (hasFixedId)
            {
                saveDataId = attribute.SaveDataId;
            }
            else
            {
                SaveDataAttribute indexerKey = attribute;

                Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out SaveDataIndexerReader tmpReader, spaceId);
                using SaveDataIndexerReader reader = tmpReader;
                if (rc.IsFailure()) return rc;

                rc = reader.Indexer.Get(out SaveDataIndexerValue indexerValue, ref indexerKey);
                if (rc.IsFailure()) return rc;

                SaveDataSpaceId indexerSpaceId = GetSpaceIdForIndexer(spaceId);

                if (indexerValue.SpaceId != indexerSpaceId)
                    return ResultFs.TargetNotFound.Log();

                if (indexerValue.State == SaveDataState.Extending)
                    return ResultFs.SaveDataIsExtending.Log();

                saveDataId = indexerValue.SaveDataId;
            }

            Result saveFsResult = FsProxyCore.OpenSaveDataFileSystem(out fileSystem, spaceId, saveDataId,
                SaveDataRootPath.ToString(), openReadOnly, attribute.Type, cacheExtraData);

            if (saveFsResult.IsSuccess()) return Result.Success;

            if (!ResultFs.PathNotFound.Includes(saveFsResult) && !ResultFs.TargetNotFound.Includes(saveFsResult)) return saveFsResult;

            if (saveDataId != FileSystemServer.SaveIndexerId)
            {
                if (hasFixedId)
                {
                    // todo: remove save indexer entry
                }
            }

            if (ResultFs.PathNotFound.Includes(saveFsResult))
            {
                return ResultFs.TargetNotFound.LogConverted(saveFsResult);
            }

            return saveFsResult;
        }

        private Result OpenSaveDataFileSystem3(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute, bool openReadOnly)
        {
            // Missing check if the open limit has been hit

            Result rc = OpenSaveDataFileSystemImpl(out fileSystem, out _, spaceId, ref attribute, openReadOnly, true);

            // Missing permission check based on the save's owner ID,
            // speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return rc;
        }

        private Result OpenSaveDataFileSystem2(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute, bool openReadOnly)
        {
            fileSystem = default;

            // Missing permission checks

            SaveDataAttribute attributeCopy;

            if (attribute.TitleId == TitleId.Zero)
            {
                throw new NotImplementedException();
            }
            else
            {
                attributeCopy = attribute;
            }

            SaveDataSpaceId actualSpaceId;

            if (attributeCopy.Type == SaveDataType.Cache)
            {
                // Check whether the save is on the SD card or the BIS
                Result rc = GetSpaceIdForCacheStorage(out actualSpaceId, attributeCopy.TitleId);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                actualSpaceId = spaceId;
            }

            return OpenSaveDataFileSystem3(out fileSystem, actualSpaceId, ref attributeCopy, openReadOnly);
        }

        public Result OpenSaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute)
        {
            return OpenSaveDataFileSystem2(out fileSystem, spaceId, ref attribute, false);
        }

        public Result OpenReadOnlySaveDataFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            return OpenSaveDataFileSystem2(out fileSystem, spaceId, ref attribute, true);
        }

        public Result OpenSaveDataFileSystemBySystemSaveDataId(out IFileSystem fileSystem, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter
            fileSystem = default;

            if (!IsSystemSaveDataId(attribute.SaveDataId)) return ResultFs.InvalidArgument.Log();

            Result rc = OpenSaveDataFileSystemImpl(out IFileSystem saveFs, out _, spaceId,
                ref attribute, false, true);
            if (rc.IsFailure()) return rc;

            // Missing check if the current title owns the save data or can open it

            fileSystem = saveFs;

            return Result.Success;
        }

        public Result ReadSaveDataFileSystemExtraData(Span<byte> extraDataBuffer, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(Span<byte> extraDataBuffer, SaveDataSpaceId spaceId,
            ref SaveDataAttribute attribute)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraDataBySaveDataAttribute(ref SaveDataAttribute attribute, SaveDataSpaceId spaceId,
            ReadOnlySpan<byte> extraDataBuffer, ReadOnlySpan<byte> maskBuffer)
        {
            throw new NotImplementedException();
        }

        public Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId, ReadOnlySpan<byte> extraDataBuffer,
            ReadOnlySpan<byte> maskBuffer)
        {
            throw new NotImplementedException();
        }

        public Result OpenImageDirectoryFileSystem(out IFileSystem fileSystem, ImageDirectoryId dirId)
        {
            throw new NotImplementedException();
        }

        public Result SetBisRootForHost(BisPartitionId partitionId, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result OpenBisFileSystem(out IFileSystem fileSystem, ref FsPath rootPath, BisPartitionId partitionId)
        {
            fileSystem = default;

            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            Result rc = PathTools.Normalize(out U8Span normalizedPath, rootPath);
            if (rc.IsFailure()) return rc;

            return FsProxyCore.OpenBisFileSystem(out fileSystem, normalizedPath.ToString(), partitionId);
        }

        public Result OpenBisStorage(out IStorage storage, BisPartitionId partitionId)
        {
            throw new NotImplementedException();
        }

        public Result InvalidateBisCache()
        {
            throw new NotImplementedException();
        }

        public Result OpenHostFileSystem(out IFileSystem fileSystem, ref FsPath subPath)
        {
            throw new NotImplementedException();
        }

        public Result OpenSdCardFileSystem(out IFileSystem fileSystem)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenSdCardFileSystem(out fileSystem);
        }

        public Result FormatSdCardFileSystem()
        {
            throw new NotImplementedException();
        }

        public Result FormatSdCardDryRun()
        {
            throw new NotImplementedException();
        }

        public Result IsExFatSupported(out bool isSupported)
        {
            throw new NotImplementedException();
        }

        public Result OpenGameCardStorage(out IStorage storage, GameCardHandle handle, GameCardPartitionRaw partitionId)
        {
            // Missing permission check and StorageInterfaceAdapter

            return FsProxyCore.OpenGameCardStorage(out storage, handle, partitionId);
        }

        public Result OpenDeviceOperator(out IDeviceOperator deviceOperator)
        {
            // Missing permission check

            return FsProxyCore.OpenDeviceOperator(out deviceOperator);
        }

        public Result OpenSaveDataInfoReader(out ISaveDataInfoReader infoReader)
        {
            infoReader = default;

            // Missing permission check

            SaveDataIndexerReader indexReader = default;

            try
            {
                Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out indexReader, SaveDataSpaceId.System);
                if (rc.IsFailure()) return rc;

                return indexReader.Indexer.OpenSaveDataInfoReader(out infoReader);
            }
            finally
            {
                indexReader.Dispose();
            }
        }

        public Result OpenSaveDataInfoReaderBySaveDataSpaceId(out ISaveDataInfoReader infoReader, SaveDataSpaceId spaceId)
        {
            infoReader = default;

            // Missing permission check

            SaveDataIndexerReader indexReader = default;

            try
            {
                Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out indexReader, spaceId);
                if (rc.IsFailure()) return rc;

                rc = indexReader.Indexer.OpenSaveDataInfoReader(out ISaveDataInfoReader baseInfoReader);
                if (rc.IsFailure()) return rc;

                var filter = new SaveDataFilterInternal
                {
                    FilterBySaveDataSpaceId = true,
                    SpaceId = GetSpaceIdForIndexer(spaceId)
                };

                infoReader = new SaveDataInfoFilterReader(baseInfoReader, ref filter);

                return Result.Success;
            }
            finally
            {
                indexReader.Dispose();
            }
        }

        public Result OpenSaveDataInfoReaderWithFilter(out ISaveDataInfoReader infoReader, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            infoReader = default;

            // Missing permission check

            SaveDataIndexerReader indexReader = default;

            try
            {
                Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out indexReader, spaceId);
                if (rc.IsFailure()) return rc;

                rc = indexReader.Indexer.OpenSaveDataInfoReader(out ISaveDataInfoReader baseInfoReader);
                if (rc.IsFailure()) return rc;

                var filterInternal = new SaveDataFilterInternal(ref filter, spaceId);

                infoReader = new SaveDataInfoFilterReader(baseInfoReader, ref filterInternal);

                return Result.Success;
            }
            finally
            {
                indexReader.Dispose();
            }
        }

        public Result FindSaveDataWithFilter(out long count, Span<byte> saveDataInfoBuffer, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            count = default;

            if (saveDataInfoBuffer.Length != Unsafe.SizeOf<SaveDataInfo>())
            {
                return ResultFs.InvalidArgument.Log();
            }

            // Missing permission check

            var internalFilter = new SaveDataFilterInternal(ref filter, GetSpaceIdForIndexer(spaceId));

            ref SaveDataInfo saveDataInfo = ref Unsafe.As<byte, SaveDataInfo>(ref saveDataInfoBuffer[0]);

            return FindSaveDataWithFilterImpl(out count, out saveDataInfo, spaceId, ref internalFilter);
        }

        private Result FindSaveDataWithFilterImpl(out long count, out SaveDataInfo info, SaveDataSpaceId spaceId,
            ref SaveDataFilterInternal filter)
        {
            count = default;
            info = default;

            SaveDataIndexerReader indexReader = default;

            try
            {
                Result rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out indexReader, spaceId);
                if (rc.IsFailure()) return rc;

                rc = indexReader.Indexer.OpenSaveDataInfoReader(out ISaveDataInfoReader baseInfoReader);
                if (rc.IsFailure()) return rc;

                var infoReader = new SaveDataInfoFilterReader(baseInfoReader, ref filter);

                return infoReader.ReadSaveDataInfo(out count, SpanHelpers.AsByteSpan(ref info));
            }
            finally
            {
                indexReader.Dispose();
            }
        }

        public Result OpenSaveDataInternalStorageFileSystem(out IFileSystem fileSystem, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataInfoReaderOnlyCacheStorage(out ISaveDataInfoReader infoReader)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataMetaFile(out IFile file, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute,
            SaveDataMetaType type)
        {
            throw new NotImplementedException();
        }

        private Result GetSpaceIdForCacheStorage(out SaveDataSpaceId spaceId, TitleId programId)
        {
            spaceId = default;

            if (FsProxyCore.IsSdCardAccessible)
            {
                var filter = new SaveDataFilterInternal();

                filter.SetSaveDataSpaceId(SaveDataSpaceId.SdCache);
                filter.SetProgramId(programId);
                filter.SetSaveDataType(SaveDataType.Cache);

                Result rc = FindSaveDataWithFilterImpl(out long count, out _, SaveDataSpaceId.SdCache, ref filter);
                if (rc.IsFailure()) return rc;

                if (count > 0)
                {
                    spaceId = SaveDataSpaceId.SdCache;
                    return Result.Success;
                }
            }

            {
                var filter = new SaveDataFilterInternal();

                filter.SetSaveDataSpaceId(SaveDataSpaceId.User);
                filter.SetProgramId(programId);
                filter.SetSaveDataType(SaveDataType.Cache);

                Result rc = FindSaveDataWithFilterImpl(out long count, out _, SaveDataSpaceId.User, ref filter);
                if (rc.IsFailure()) return rc;

                if (count > 0)
                {
                    spaceId = SaveDataSpaceId.User;
                    return Result.Success;
                }
            }

            return ResultFs.TargetNotFound.Log();
        }

        public Result DeleteCacheStorage(short index)
        {
            throw new NotImplementedException();
        }

        public Result GetCacheStorageSize(out long dataSize, out long journalSize, short index)
        {
            throw new NotImplementedException();
        }

        public Result ListAccessibleSaveDataOwnerId(out int readCount, Span<TitleId> idBuffer, TitleId programId, int startIndex,
            int bufferIdCount)
        {
            throw new NotImplementedException();
        }

        public Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize)
        {
            if (saveDataSize < 0 || saveDataJournalSize < 0)
            {
                return ResultFs.InvalidSize.Log();
            }

            SaveDataSize = saveDataSize;
            SaveDataJournalSize = saveDataJournalSize;

            return Result.Success;
        }
        public Result SetSaveDataRootPath(ref FsPath path)
        {
            // Missing permission check

            if (StringUtils.GetLength(path.Str, FsPath.MaxLength + 1) > FsPath.MaxLength)
            {
                return ResultFs.TooLongPath.Log();
            }

            StringUtils.Copy(SaveDataRootPath.Str, path.Str);

            return Result.Success;
        }

        public Result OpenContentStorageFileSystem(out IFileSystem fileSystem, ContentStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenContentStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenCloudBackupWorkStorageFileSystem(out IFileSystem fileSystem, CloudBackupWorkStorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenCustomStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenGameCardFileSystem(out IFileSystem fileSystem, GameCardHandle handle,
            GameCardPartition partitionId)
        {
            // Missing permission check and FileSystemInterfaceAdapter

            return FsProxyCore.OpenGameCardFileSystem(out fileSystem, handle, partitionId);
        }

        public Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize)
        {
            // todo: Implement properly

            totalSize = 0;

            return Result.Success;
        }

        public Result SetCurrentPosixTimeWithTimeDifference(long time, int difference)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsId(out RightsId rightsId, TitleId programId, StorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsIdByPath(out RightsId rightsId, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result GetRightsIdAndKeyGenerationByPath(out RightsId rightsId, out byte keyGeneration, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result RegisterExternalKey(ref RightsId rightsId, ref AccessKey externalKey)
        {
            // Missing permission check

            return FsProxyCore.RegisterExternalKey(ref rightsId, ref externalKey);
        }

        public Result UnregisterExternalKey(ref RightsId rightsId)
        {
            // Missing permission check

            return FsProxyCore.UnregisterExternalKey(ref rightsId);
        }

        public Result UnregisterAllExternalKey()
        {
            // Missing permission check

            return FsProxyCore.UnregisterAllExternalKey();
        }

        public Result SetSdCardEncryptionSeed(ref EncryptionSeed seed)
        {
            // Missing permission check

            Result rc = FsProxyCore.SetSdCardEncryptionSeed(ref seed);
            if (rc.IsFailure()) return rc;

            FsServer.SaveDataIndexerManager.ResetSdCardIndexer();

            return Result.Success;
        }

        public Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId, Span<byte> readBuffer)
        {
            throw new NotImplementedException();
        }

        public Result VerifySaveDataFileSystem(ulong saveDataId, Span<byte> readBuffer)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result CorruptSaveDataFileSystem(ulong saveDataId)
        {
            throw new NotImplementedException();
        }

        public Result CreatePaddingFile(long size)
        {
            throw new NotImplementedException();
        }

        public Result DeleteAllPaddingFiles()
        {
            throw new NotImplementedException();
        }

        public Result DisableAutoSaveDataCreation()
        {
            AutoCreateSaveData = false;

            return Result.Success;
        }

        public Result SetGlobalAccessLogMode(GlobalAccessLogMode mode)
        {
            // Missing permission check

            return FsProxyCore.SetGlobalAccessLogMode(mode);
        }

        public Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode)
        {
            return FsProxyCore.GetGlobalAccessLogMode(out mode);
        }

        public Result GetProgramIndexForAccessLog(out int programIndex, out int programCount)
        {
            throw new NotImplementedException();
        }

        public Result OutputAccessLogToSdCard(U8Span logString)
        {
            throw new NotImplementedException();
        }

        public Result RegisterUpdatePartition()
        {
            throw new NotImplementedException();
        }

        public Result OpenRegisteredUpdatePartition(out IFileSystem fileSystem)
        {
            throw new NotImplementedException();
        }

        public Result OverrideSaveDataTransferTokenSignVerificationKey(ReadOnlySpan<byte> key)
        {
            throw new NotImplementedException();
        }

        public Result CleanUpTemporaryStorage()
        {
            Result rc = FsProxyCore.OpenSaveDataDirectory(out IFileSystem saveDirFs, SaveDataSpaceId.Temporary,
                string.Empty, false);
            if (rc.IsFailure()) return rc;

            rc = saveDirFs.CleanDirectoryRecursively("/".ToU8Span());
            if (rc.IsFailure()) return rc;

            SaveDataIndexerReader reader = default;

            try
            {
                rc = FsServer.SaveDataIndexerManager.GetSaveDataIndexer(out reader, SaveDataSpaceId.Temporary);
                if (rc.IsFailure()) return rc;

                reader.Indexer.Reset();

                return Result.Success;
            }
            finally
            {
                reader.Dispose();
            }
        }

        public Result SetSdCardAccessibility(bool isAccessible)
        {
            // Missing permission check

            FsProxyCore.IsSdCardAccessible = isAccessible;
            return Result.Success;
        }

        public Result IsSdCardAccessible(out bool isAccessible)
        {
            isAccessible = FsProxyCore.IsSdCardAccessible;
            return Result.Success;
        }

        private static bool IsSystemSaveDataId(ulong id)
        {
            return (long)id < 0;
        }

        private static SaveDataSpaceId GetSpaceIdForIndexer(SaveDataSpaceId spaceId)
        {
            return spaceId == SaveDataSpaceId.ProperSystem || spaceId == SaveDataSpaceId.SafeMode
                ? SaveDataSpaceId.System
                : spaceId;
        }
    }
}
