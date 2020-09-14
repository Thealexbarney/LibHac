using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Kvdb;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Spl;
using LibHac.Util;

namespace LibHac.FsSrv
{
    public class FileSystemProxyImpl : IFileSystemProxy, IFileSystemProxyForLoader
    {
        private FileSystemProxyCoreImpl FsProxyCore { get; }
        private ReferenceCountedDisposable<NcaFileSystemService> NcaFsService { get; set; }
        internal HorizonClient Hos { get; }

        public ulong CurrentProcess { get; private set; }

        public long SaveDataSize { get; private set; }
        public long SaveDataJournalSize { get; private set; }
        public FsPath SaveDataRootPath { get; }
        public bool AutoCreateSaveData { get; private set; }

        internal FileSystemProxyImpl(HorizonClient horizonClient, FileSystemProxyCoreImpl fsProxyCore)
        {
            FsProxyCore = fsProxyCore;
            Hos = horizonClient;

            CurrentProcess = ulong.MaxValue;
            SaveDataSize = 0x2000000;
            SaveDataJournalSize = 0x1000000;
            AutoCreateSaveData = true;
        }

        private ProgramRegistryService GetProgramRegistryService()
        {
            return new ProgramRegistryService(FsProxyCore.Config.ProgramRegistryService, CurrentProcess);
        }

        public Result OpenFileSystemWithId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath path,
            ulong id, FileSystemProxyType fsType)
        {

            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return ncaFsService.OpenFileSystemWithId(out fileSystem, in path, id, fsType);
        }

        public Result OpenFileSystemWithPatch(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ProgramId programId, FileSystemProxyType fsType)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return ncaFsService.OpenFileSystemWithPatch(out fileSystem, programId, fsType);
        }

        public Result OpenCodeFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            out CodeVerificationData verificationData, in FspPath path, ProgramId programId)
        {
            Unsafe.SkipInit(out verificationData);

            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return ncaFsService.OpenCodeFileSystem(out fileSystem, out verificationData, in path, programId);
        }

        public Result IsArchivedProgram(out bool isArchived, ulong processId)
        {
            Unsafe.SkipInit(out isArchived);

            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.IsArchivedProgram(out isArchived, processId);
        }

        public Result SetCurrentProcess(ulong processId)
        {
            CurrentProcess = processId;

            // Initialize the NCA file system service
            NcaFsService = NcaFileSystemService.Create(FsProxyCore.Config.NcaFileSystemService, processId);

            return Result.Success;
        }

        public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
        {
            throw new NotImplementedException();
        }

        public Result OpenDataFileSystemByCurrentProcess(out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return ncaFsService.OpenDataFileSystemByCurrentProcess(out fileSystem);
        }

        public Result OpenDataFileSystemByProgramId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ProgramId programId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return ncaFsService.OpenDataFileSystemByProgramId(out fileSystem, programId);
        }

        public Result OpenDataStorageByCurrentProcess(out ReferenceCountedDisposable<IStorageSf> storage)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                storage = default;
                return rc;
            }

            return ncaFsService.OpenDataStorageByCurrentProcess(out storage);
        }

        public Result OpenDataStorageByProgramId(out ReferenceCountedDisposable<IStorageSf> storage, ProgramId programId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                storage = default;
                return rc;
            }

            return ncaFsService.OpenDataStorageByProgramId(out storage, programId);
        }

        public Result OpenDataStorageByDataId(out ReferenceCountedDisposable<IStorageSf> storage, DataId dataId, StorageId storageId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                storage = default;
                return rc;
            }

            return ncaFsService.OpenDataStorageByDataId(out storage, dataId, storageId);
        }

        public Result OpenPatchDataStorageByCurrentProcess(out ReferenceCountedDisposable<IStorageSf> storage)
        {
            storage = default;
            return ResultFs.TargetNotFound.Log();
        }

        public Result OpenDataFileSystemWithProgramIndex(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            byte programIndex)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return ncaFsService.OpenDataFileSystemWithProgramIndex(out fileSystem, programIndex);
        }

        public Result OpenDataStorageWithProgramIndex(out ReferenceCountedDisposable<IStorageSf> storage, byte programIndex)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                storage = default;
                return rc;
            }

            return ncaFsService.OpenDataStorageWithProgramIndex(out storage, programIndex);
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
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            SaveDataIndexerAccessor accessor = null;

            try
            {
                SaveDataType saveType;

                if (saveDataId == FileSystemServer.SaveIndexerId)
                {
                    if (!IsCurrentProcess(CurrentProcess))
                        return ResultFs.PermissionDenied.Log();

                    saveType = SaveDataType.System;
                }
                else
                {
                    rc = OpenSaveDataIndexerAccessor(out accessor, spaceId);
                    if (rc.IsFailure()) return rc;

                    if (spaceId != SaveDataSpaceId.ProperSystem && spaceId != SaveDataSpaceId.SafeMode)
                    {
                        rc = accessor.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                        if (rc.IsFailure()) return rc;

                        spaceId = value.SpaceId;
                    }

                    rc = accessor.Indexer.GetKey(out SaveDataAttribute key, saveDataId);
                    if (rc.IsFailure()) return rc;

                    if (key.Type == SaveDataType.System || key.Type == SaveDataType.SystemBcat)
                    {
                        if (!programInfo.AccessControl.CanCall(OperationType.DeleteSystemSaveData))
                            return ResultFs.PermissionDenied.Log();
                    }
                    else
                    {
                        if (!programInfo.AccessControl.CanCall(OperationType.DeleteSaveData))
                            return ResultFs.PermissionDenied.Log();
                    }

                    saveType = key.Type;

                    rc = accessor.Indexer.SetState(saveDataId, SaveDataState.Creating);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                rc = DeleteSaveDataFileSystemImpl2(spaceId, saveDataId, saveType, false);
                if (rc.IsFailure()) return rc;

                if (saveDataId != FileSystemServer.SaveIndexerId)
                {
                    // ReSharper disable once PossibleNullReferenceException
                    rc = accessor.Indexer.Delete(saveDataId);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }
            finally { accessor?.Dispose(); }
        }

        // ReSharper disable once UnusedParameter.Local
        private Result DeleteSaveDataFileSystemImpl2(SaveDataSpaceId spaceId, ulong saveDataId, SaveDataType type, bool shouldWipe)
        {
            // missing: Check extra data flags for this value. Bit 3
            bool doSecureDelete = shouldWipe;

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
                Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
                if (rc.IsFailure()) return rc;

                using (accessor)
                {
                    rc = accessor.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);
                    if (rc.IsFailure()) return rc;

                    if (value.SpaceId != GetSpaceIdForIndexer(spaceId))
                        return ResultFs.TargetNotFound.Log();
                }
            }

            return DeleteSaveDataFileSystemImpl(spaceId, saveDataId);
        }

        private Result GetSaveDataInfo(out SaveDataInfo info, SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            Unsafe.SkipInit(out info);

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.Get(out SaveDataIndexerValue value, in attribute);
                if (rc.IsFailure()) return rc;

                SaveDataIndexer.GenerateSaveDataInfo(out info, in attribute, in value);
                return Result.Success;
            }
        }

        public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, ref SaveDataAttribute attribute)
        {
            Result rs = GetSaveDataInfo(out SaveDataInfo info, spaceId, in attribute);
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

            SaveDataIndexerAccessor accessor = null;

            // Missing permission checks

            try
            {
                if (attribute.StaticSaveDataId == FileSystemServer.SaveIndexerId)
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
                    rc = OpenSaveDataIndexerAccessor(out accessor, creationInfo.SpaceId);
                    if (rc.IsFailure()) return rc;

                    SaveDataAttribute indexerKey = attribute;

                    if (attribute.StaticSaveDataId != 0 && attribute.UserId == UserId.Zero)
                    {
                        saveDataId = attribute.StaticSaveDataId;

                        rc = accessor.Indexer.PutStaticSaveDataIdIndex(in indexerKey);
                    }
                    else
                    {
                        if (attribute.Type != SaveDataType.System &&
                            attribute.Type != SaveDataType.SystemBcat)
                        {
                            if (accessor.Indexer.IsRemainedReservedOnly())
                            {
                                return ResultKvdb.OutOfKeyResource.Log();
                            }
                        }

                        rc = accessor.Indexer.Publish(out saveDataId, in indexerKey);
                    }

                    if (ResultFs.SaveDataPathAlreadyExists.Includes(rc))
                    {
                        return ResultFs.PathAlreadyExists.LogConverted(rc);
                    }

                    isDeleteNeeded = true;

                    rc = accessor.Indexer.SetState(saveDataId, SaveDataState.Creating);
                    if (rc.IsFailure()) return rc;

                    SaveDataSpaceId indexerSpaceId = GetSpaceIdForIndexer(creationInfo.SpaceId);

                    rc = accessor.Indexer.SetSpaceId(saveDataId, indexerSpaceId);
                    if (rc.IsFailure()) return rc;

                    // todo: calculate size
                    long size = 0;

                    rc = accessor.Indexer.SetSize(saveDataId, size);
                    if (rc.IsFailure()) return rc;

                    rc = accessor.Indexer.Commit();
                    if (rc.IsFailure()) return rc;
                }

                rc = FsProxyCore.CreateSaveDataFileSystem(saveDataId, ref attribute, ref creationInfo, SaveDataRootPath,
                    hashSalt, false);

                if (rc.IsFailure())
                {
                    if (!ResultFs.PathAlreadyExists.Includes(rc)) return rc;

                    rc = DeleteSaveDataFileSystemImpl2(creationInfo.SpaceId, saveDataId, attribute.Type, false);
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

                if (attribute.StaticSaveDataId == FileSystemServer.SaveIndexerId || something)
                {
                    isDeleteNeeded = false;

                    return Result.Success;
                }

                // accessor shouldn't ever be null, but checking makes the analyzers happy
                Abort.DoAbortUnless(accessor != null);

                rc = accessor.Indexer.SetState(saveDataId, SaveDataState.Normal);
                if (rc.IsFailure()) return rc;

                rc = accessor.Indexer.Commit();
                if (rc.IsFailure()) return rc;

                isDeleteNeeded = false;

                return Result.Success;
            }
            finally
            {
                // Revert changes if an error happened in the middle of creation
                if (isDeleteNeeded)
                {
                    DeleteSaveDataFileSystemImpl2(creationInfo.SpaceId, saveDataId, attribute.Type, false).IgnoreResult();

                    if (accessor != null && saveDataId != FileSystemServer.SaveIndexerId)
                    {
                        rc = accessor.Indexer.GetValue(out SaveDataIndexerValue value, saveDataId);

                        if (rc.IsSuccess() && value.SpaceId == creationInfo.SpaceId)
                        {
                            accessor.Indexer.Delete(saveDataId).IgnoreResult();
                            accessor.Indexer.Commit().IgnoreResult();
                        }
                    }
                }

                accessor?.Dispose();
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
            if (!IsSystemSaveDataId(attribute.StaticSaveDataId))
                return ResultFs.InvalidArgument.Log();

            SaveDataCreationInfo newCreationInfo = creationInfo;

            if (creationInfo.OwnerId == 0)
            {
                // todo: Assign the current program's ID
                // throw new NotImplementedException();
            }

            // Missing permission checks

            if (attribute.Type == SaveDataType.SystemBcat)
            {
                newCreationInfo.OwnerId = SystemProgramId.Bcat.Value;
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

            bool hasFixedId = attribute.StaticSaveDataId != 0 && attribute.UserId == UserId.Zero;

            if (hasFixedId)
            {
                saveDataId = attribute.StaticSaveDataId;
            }
            else
            {
                SaveDataAttribute indexerKey = attribute;

                Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor tempAccessor, spaceId);
                using SaveDataIndexerAccessor accessor = tempAccessor;
                if (rc.IsFailure()) return rc;

                rc = accessor.Indexer.Get(out SaveDataIndexerValue indexerValue, in indexerKey);
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

            if (attribute.ProgramId == ProgramId.InvalidId)
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
                Result rc = GetSpaceIdForCacheStorage(out actualSpaceId, attributeCopy.ProgramId);
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

            if (!IsSystemSaveDataId(attribute.StaticSaveDataId)) return ResultFs.InvalidArgument.Log();

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

        public Result OpenImageDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ImageDirectoryId directoryId)
        {
            return GetBaseFileSystemService().OpenImageDirectoryFileSystem(out fileSystem, directoryId);
        }

        public Result RegisterProgramIndexMapInfo(ReadOnlySpan<byte> programIndexMapInfoBuffer, int programCount)
        {
            return GetProgramRegistryService().RegisterProgramIndexMapInfo(programIndexMapInfoBuffer, programCount);
        }

        public Result SetBisRootForHost(BisPartitionId partitionId, ref FsPath path)
        {
            throw new NotImplementedException();
        }

        public Result OpenBisFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath rootPath,
            BisPartitionId partitionId)
        {
            return GetBaseFileSystemService().OpenBisFileSystem(out fileSystem, in rootPath, partitionId);
        }

        public Result OpenBisStorage(out ReferenceCountedDisposable<IStorageSf> storage, BisPartitionId partitionId)
        {
            throw new NotImplementedException();
        }

        public Result InvalidateBisCache()
        {
            throw new NotImplementedException();
        }

        public Result OpenHostFileSystemWithOption(out IFileSystem fileSystem, ref FsPath path, MountHostOption option)
        {
            // Missing permission check

            return FsProxyCore.OpenHostFileSystem(out fileSystem, new U8Span(path.Str), option.HasFlag(MountHostOption.PseudoCaseSensitive));
        }

        public Result OpenHostFileSystem(out IFileSystem fileSystem, ref FsPath path)
        {
            return OpenHostFileSystemWithOption(out fileSystem, ref path, MountHostOption.None);
        }

        public Result OpenSdCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            return GetBaseFileSystemService().OpenSdCardFileSystem(out fileSystem);
        }

        public Result FormatSdCardFileSystem()
        {
            return GetBaseFileSystemService().FormatSdCardFileSystem();
        }

        public Result FormatSdCardDryRun()
        {
            return GetBaseFileSystemService().FormatSdCardDryRun();
        }

        public Result IsExFatSupported(out bool isSupported)
        {
            return GetBaseFileSystemService().IsExFatSupported(out isSupported);
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

        public Result OpenSystemDataUpdateEventNotifier(out ReferenceCountedDisposable<IEventNotifier> eventNotifier)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                eventNotifier = null;
                return rc;
            }

            return ncaFsService.OpenSystemDataUpdateEventNotifier(out eventNotifier);
        }

        public Result NotifySystemDataUpdateEvent()
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.NotifySystemDataUpdateEvent();
        }

        public Result OpenSaveDataInfoReader(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader)
        {
            infoReader = default;

            // Missing permission check

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, SaveDataSpaceId.System);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                return accessor.Indexer.OpenSaveDataInfoReader(out infoReader);
            }
        }

        public Result OpenSaveDataInfoReaderBySaveDataSpaceId(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId)
        {
            infoReader = default;

            // Missing permission check

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.OpenSaveDataInfoReader(
                    out ReferenceCountedDisposable<ISaveDataInfoReader> baseInfoReader);
                if (rc.IsFailure()) return rc;

                var filter = new SaveDataFilterInternal
                {
                    FilterBySaveDataSpaceId = true,
                    SpaceId = GetSpaceIdForIndexer(spaceId)
                };

                var filterReader = new SaveDataInfoFilterReader(baseInfoReader, ref filter);
                infoReader = new ReferenceCountedDisposable<ISaveDataInfoReader>(filterReader);

                return Result.Success;
            }
        }

        public Result OpenSaveDataInfoReaderWithFilter(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId,
            ref SaveDataFilter filter)
        {
            infoReader = default;

            // Missing permission check

            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.OpenSaveDataInfoReader(
                    out ReferenceCountedDisposable<ISaveDataInfoReader> baseInfoReader);
                if (rc.IsFailure()) return rc;

                var filterInternal = new SaveDataFilterInternal(ref filter, spaceId);

                var filterReader = new SaveDataInfoFilterReader(baseInfoReader, ref filterInternal);
                infoReader = new ReferenceCountedDisposable<ISaveDataInfoReader>(filterReader);

                return Result.Success;
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


            Result rc = OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, spaceId);
            if (rc.IsFailure()) return rc;

            using (accessor)
            {
                rc = accessor.Indexer.OpenSaveDataInfoReader(
                    out ReferenceCountedDisposable<ISaveDataInfoReader> baseInfoReader);
                if (rc.IsFailure()) return rc;

                using (var infoReader = new SaveDataInfoFilterReader(baseInfoReader, ref filter))
                {
                    return infoReader.Read(out count, SpanHelpers.AsByteSpan(ref info));
                }
            }
        }

        public Result OpenSaveDataInternalStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId)
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

        public Result OpenSaveDataInfoReaderOnlyCacheStorage(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader)
        {
            throw new NotImplementedException();
        }

        public Result OpenSaveDataMetaFile(out IFile file, SaveDataSpaceId spaceId, ref SaveDataAttribute attribute,
            SaveDataMetaType type)
        {
            throw new NotImplementedException();
        }

        private Result GetSpaceIdForCacheStorage(out SaveDataSpaceId spaceId, ProgramId programId)
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

        public Result ListAccessibleSaveDataOwnerId(out int readCount, Span<Ncm.ApplicationId> idBuffer, ProgramId programId, int startIndex,
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

        public Result OpenContentStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, ContentStorageId storageId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = null;
                return rc;
            }

            return ncaFsService.OpenContentStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenCloudBackupWorkStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            CloudBackupWorkStorageId storageId)
        {
            throw new NotImplementedException();
        }

        public Result OpenCustomStorageFileSystem(out IFileSystem fileSystem, CustomStorageId storageId)
        {
            // Missing permission check, speed emulation storage type wrapper, and FileSystemInterfaceAdapter

            return FsProxyCore.OpenCustomStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenGameCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            GameCardHandle handle, GameCardPartition partitionId)
        {
            return GetBaseFileSystemService().OpenGameCardFileSystem(out fileSystem, handle, partitionId);
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

        public Result GetRightsId(out RightsId rightsId, ProgramId programId, StorageId storageId)
        {
            Unsafe.SkipInit(out rightsId);

            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.GetRightsId(out rightsId, programId, storageId);
        }

        public Result GetRightsIdByPath(out RightsId rightsId, in FspPath path)
        {
            return GetRightsIdAndKeyGenerationByPath(out rightsId, out _, in path);
        }

        public Result GetRightsIdAndKeyGenerationByPath(out RightsId rightsId, out byte keyGeneration, in FspPath path)
        {
            Unsafe.SkipInit(out rightsId);
            Unsafe.SkipInit(out keyGeneration);

            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, in path);
        }

        public Result RegisterExternalKey(in RightsId rightsId, in AccessKey externalKey)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.RegisterExternalKey(in rightsId, in externalKey);
        }

        public Result UnregisterExternalKey(in RightsId rightsId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.UnregisterExternalKey(in rightsId);
        }

        public Result UnregisterAllExternalKey()
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.UnregisterAllExternalKey();
        }

        public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.SetEncryptionSeed))
                return ResultFs.PermissionDenied.Log();

            rc = FsProxyCore.SetSdCardEncryptionSeed(in seed);
            if (rc.IsFailure()) return rc;

            rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.SetSdCardEncryptionSeed(in seed);
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
            return GetBaseFileSystemService().CreatePaddingFile(size);
        }

        public Result DeleteAllPaddingFiles()
        {
            return GetBaseFileSystemService().DeleteAllPaddingFiles();
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
            return GetProgramRegistryService().GetProgramIndexForAccessLog(out programIndex, out programCount);
        }

        public Result OutputAccessLogToSdCard(U8Span logString)
        {
            throw new NotImplementedException();
        }

        public Result RegisterUpdatePartition()
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.RegisterUpdatePartition();
        }

        public Result OpenRegisteredUpdatePartition(out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return ncaFsService.OpenRegisteredUpdatePartition(out fileSystem);
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

            FsProxyCore.SaveDataIndexerManager.ResetTemporaryStorageIndexer(SaveDataSpaceId.Temporary);

            return Result.Success;
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

        public Result OpenMultiCommitManager(out IMultiCommitManager commitManager)
        {
            commitManager = new MultiCommitManager(this);
            return Result.Success;
        }

        public Result OpenBisWiper(out ReferenceCountedDisposable<IWiper> bisWiper, NativeHandle transferMemoryHandle,
            ulong transferMemorySize)
        {
            return GetBaseFileSystemService().OpenBisWiper(out bisWiper, transferMemoryHandle, transferMemorySize);
        }

        internal Result OpenMultiCommitContextSaveData(out IFileSystem fileSystem)
        {
            fileSystem = default;

            var attribute = new SaveDataAttribute(new ProgramId(MultiCommitManager.ProgramId), SaveDataType.System,
                UserId.Zero, MultiCommitManager.SaveDataId);

            Result rc = OpenSaveDataFileSystemImpl(out IFileSystem saveFs, out _, SaveDataSpaceId.System, ref attribute,
                false, true);
            if (rc.IsFailure()) return rc;

            fileSystem = saveFs;
            return Result.Success;
        }

        // todo: split the FileSystemProxy classes
        // nn::fssrv::SaveDataFileSystemService::GetSaveDataIndexerAccessor
        private Result OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessor, SaveDataSpaceId spaceId)
        {
            accessor = default;

            Result rc = FsProxyCore.OpenSaveDataIndexerAccessor(out SaveDataIndexerAccessor accessorTemp,
                out bool neededInit, spaceId);
            if (rc.IsFailure()) return rc;

            try
            {
                if (neededInit)
                {
                    // todo: nn::fssrv::SaveDataFileSystemService::CleanUpSaveDataCore
                    // nn::fssrv::SaveDataFileSystemService::CompleteSaveDataExtensionCore
                }

                accessor = accessorTemp;
                accessorTemp = null;

                return Result.Success;
            }
            finally
            {
                accessorTemp?.Dispose();
            }
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return FsProxyCore.ProgramRegistry.GetProgramInfo(out programInfo, CurrentProcess);
        }

        private BaseFileSystemService GetBaseFileSystemService()
        {
            return new BaseFileSystemService(FsProxyCore.Config.BaseFileSystemService, CurrentProcess);
        }

        private Result GetNcaFileSystemService(out NcaFileSystemService ncaFsService)
        {
            if (NcaFsService is null)
            {
                ncaFsService = null;
                return ResultFs.PreconditionViolation.Log();
            }

            ncaFsService = NcaFsService.Target;
            return Result.Success;
        }

        internal bool IsCurrentProcess(ulong processId)
        {
            ulong currentId = Hos.Os.GetCurrentProcessId().Value;

            return processId == currentId;
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
