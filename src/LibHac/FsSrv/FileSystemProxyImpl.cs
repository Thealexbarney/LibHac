using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Spl;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv
{
    public class FileSystemProxyImpl : IFileSystemProxy, IFileSystemProxyForLoader
    {
        private FileSystemProxyCoreImpl FsProxyCore { get; }
        private ReferenceCountedDisposable<NcaFileSystemService> NcaFsService { get; set; }
        private ReferenceCountedDisposable<SaveDataFileSystemService> SaveFsService { get; set; }
        private ulong CurrentProcess { get; set; }

        internal FileSystemProxyImpl(FileSystemProxyCoreImpl fsProxyCore)
        {
            FsProxyCore = fsProxyCore;
            CurrentProcess = ulong.MaxValue;
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
            NcaFsService = NcaFileSystemService.CreateShared(FsProxyCore.Config.NcaFileSystemService, processId);

            SaveFsService = SaveDataFileSystemService.CreateShared(FsProxyCore.Config.SaveDataFileSystemService, processId);

            return Result.Success;
        }

        public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
        {
            Unsafe.SkipInit(out freeSpaceSize);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.GetFreeSpaceSizeForSaveData(out freeSpaceSize, spaceId);
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

        public Result RegisterSaveDataFileSystemAtomicDeletion(InBuffer saveDataIds)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.RegisterSaveDataFileSystemAtomicDeletion(saveDataIds);
        }

        public Result DeleteSaveDataFileSystem(ulong saveDataId)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.DeleteSaveDataFileSystem(saveDataId);
        }

        public Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.DeleteSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);
        }

        public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.DeleteSaveDataFileSystemBySaveDataAttribute(spaceId, in attribute);
        }

        public Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.UpdateSaveDataMacForDebug(spaceId, saveDataId);
        }

        public Result CreateSaveDataFileSystem(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo,
            in SaveDataMetaInfo metaInfo)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
        }

        public Result CreateSaveDataFileSystemWithHashSalt(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in HashSalt hashSalt)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.CreateSaveDataFileSystemWithHashSalt(in attribute, in creationInfo, in metaInfo,
                in hashSalt);
        }

        public Result CreateSaveDataFileSystemBySystemSaveDataId(in SaveDataAttribute attribute,
            in SaveDataCreationInfo creationInfo)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.CreateSaveDataFileSystemBySystemSaveDataId(in attribute, in creationInfo);
        }

        public Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize,
            long journalSize)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.ExtendSaveDataFileSystem(spaceId, saveDataId, dataSize, journalSize);
        }

        public Result OpenSaveDataFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return saveFsService.OpenSaveDataFileSystem(out fileSystem, spaceId, in attribute);
        }

        public Result OpenReadOnlySaveDataFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return saveFsService.OpenReadOnlySaveDataFileSystem(out fileSystem, spaceId, in attribute);
        }

        public Result OpenSaveDataFileSystemBySystemSaveDataId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return saveFsService.OpenSaveDataFileSystemBySystemSaveDataId(out fileSystem, spaceId, in attribute);
        }

        public Result ReadSaveDataFileSystemExtraData(OutBuffer extraDataBuffer, ulong saveDataId)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.ReadSaveDataFileSystemExtraData(extraDataBuffer, saveDataId);
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(OutBuffer extraDataBuffer,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.ReadSaveDataFileSystemExtraDataBySaveDataAttribute(extraDataBuffer, spaceId,
                in attribute);
        }

        public Result ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(OutBuffer extraDataBuffer,
            SaveDataSpaceId spaceId, in SaveDataAttribute attribute, InBuffer maskBuffer)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(extraDataBuffer, spaceId,
                in attribute, maskBuffer);
        }

        public Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(OutBuffer extraDataBuffer,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(extraDataBuffer, spaceId, saveDataId);
        }

        public Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId,
            InBuffer extraDataBuffer)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.WriteSaveDataFileSystemExtraData(saveDataId, spaceId, extraDataBuffer);
        }

        public Result WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in SaveDataAttribute attribute,
            SaveDataSpaceId spaceId, InBuffer extraDataBuffer, InBuffer maskBuffer)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in attribute, spaceId,
                extraDataBuffer, maskBuffer);
        }

        public Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId,
            InBuffer extraDataBuffer, InBuffer maskBuffer)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.WriteSaveDataFileSystemExtraDataWithMask(saveDataId, spaceId, extraDataBuffer,
                maskBuffer);
        }

        public Result OpenImageDirectoryFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ImageDirectoryId directoryId)
        {
            return GetBaseFileSystemService().OpenImageDirectoryFileSystem(out fileSystem, directoryId);
        }

        public Result OpenBisFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath rootPath,
            BisPartitionId partitionId)
        {
            return GetBaseFileSystemService().OpenBisFileSystem(out fileSystem, in rootPath, partitionId);
        }

        public Result OpenBisStorage(out ReferenceCountedDisposable<IStorageSf> storage, BisPartitionId partitionId)
        {
            return GetBaseStorageService().OpenBisStorage(out storage, partitionId);
        }

        public Result InvalidateBisCache()
        {
            return GetBaseStorageService().InvalidateBisCache();
        }

        public Result OpenHostFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath path)
        {
            return OpenHostFileSystemWithOption(out fileSystem, in path, MountHostOption.None);
        }

        public Result OpenHostFileSystemWithOption(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            in FspPath path, MountHostOption option)
        {
            fileSystem = default;

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountHost);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> hostFs = null;
            try
            {
                rc = FsProxyCore.OpenHostFileSystem(out hostFs, new U8Span(path.Str),
                    option.HasFlag(MountHostOption.PseudoCaseSensitive));
                if (rc.IsFailure()) return rc;

                bool isRootPath = path.Str[0] == 0;

                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref hostFs, isRootPath);

                if (fileSystem is null)
                    return ResultFs.AllocationMemoryFailedInCreateShared.Log();

                return Result.Success;
            }
            finally
            {
                hostFs?.Dispose();
            }
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

        public Result OpenGameCardStorage(out ReferenceCountedDisposable<IStorageSf> storage, GameCardHandle handle,
            GameCardPartitionRaw partitionId)
        {
            return GetBaseStorageService().OpenGameCardStorage(out storage, handle, partitionId);
        }

        public Result OpenDeviceOperator(out ReferenceCountedDisposable<IDeviceOperator> deviceOperator)
        {
            return GetBaseStorageService().OpenDeviceOperator(out deviceOperator);
        }

        public Result OpenSdCardDetectionEventNotifier(out ReferenceCountedDisposable<IEventNotifier> eventNotifier)
        {
            return GetBaseStorageService().OpenSdCardDetectionEventNotifier(out eventNotifier);
        }

        public Result OpenGameCardDetectionEventNotifier(out ReferenceCountedDisposable<IEventNotifier> eventNotifier)
        {
            return GetBaseStorageService().OpenGameCardDetectionEventNotifier(out eventNotifier);
        }

        public Result SimulateDeviceDetectionEvent(SdmmcPort port, SimulatingDeviceDetectionMode mode, bool signalEvent)
        {
            return GetBaseStorageService().SimulateDeviceDetectionEvent(port, mode, signalEvent);
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
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                infoReader = default;
                return rc;
            }

            return saveFsService.OpenSaveDataInfoReader(out infoReader);
        }

        public Result OpenSaveDataInfoReaderBySaveDataSpaceId(
            out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader, SaveDataSpaceId spaceId)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                infoReader = default;
                return rc;
            }

            return saveFsService.OpenSaveDataInfoReaderBySaveDataSpaceId(out infoReader, spaceId);
        }

        public Result OpenSaveDataInfoReaderWithFilter(out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader,
            SaveDataSpaceId spaceId, in SaveDataFilter filter)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                infoReader = default;
                return rc;
            }

            return saveFsService.OpenSaveDataInfoReaderWithFilter(out infoReader, spaceId, in filter);
        }

        public Result FindSaveDataWithFilter(out long count, OutBuffer saveDataInfoBuffer, SaveDataSpaceId spaceId,
            in SaveDataFilter filter)
        {
            Unsafe.SkipInit(out count);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.FindSaveDataWithFilter(out count, saveDataInfoBuffer, spaceId, in filter);
        }

        public Result OpenSaveDataInternalStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                fileSystem = default;
                return rc;
            }

            return saveFsService.OpenSaveDataInternalStorageFileSystem(out fileSystem, spaceId, saveDataId);
        }

        public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Unsafe.SkipInit(out size);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
        }

        public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            Unsafe.SkipInit(out commitId);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.GetSaveDataCommitId(out commitId, spaceId, saveDataId);
        }

        public Result OpenSaveDataInfoReaderOnlyCacheStorage(
            out ReferenceCountedDisposable<ISaveDataInfoReader> infoReader)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                infoReader = default;
                return rc;
            }

            return saveFsService.OpenSaveDataInfoReaderOnlyCacheStorage(out infoReader);
        }

        public Result OpenSaveDataMetaFile(out ReferenceCountedDisposable<IFileSf> file, SaveDataSpaceId spaceId,
            in SaveDataAttribute attribute, SaveDataMetaType type)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                file = default;
                return rc;
            }

            return saveFsService.OpenSaveDataMetaFile(out file, spaceId, in attribute, type);
        }

        public Result DeleteCacheStorage(ushort index)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.DeleteCacheStorage(index);
        }

        public Result GetCacheStorageSize(out long dataSize, out long journalSize, ushort index)
        {
            Unsafe.SkipInit(out dataSize);
            Unsafe.SkipInit(out journalSize);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.GetCacheStorageSize(out dataSize, out journalSize, index);
        }

        public Result ListAccessibleSaveDataOwnerId(out int readCount, OutBuffer idBuffer, ProgramId programId,
            int startIndex, int bufferIdCount)
        {
            Unsafe.SkipInit(out readCount);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.ListAccessibleSaveDataOwnerId(out readCount, idBuffer, programId, startIndex,
                bufferIdCount);
        }

        public Result OpenSaveDataMover(out ReferenceCountedDisposable<ISaveDataMover> saveMover,
            SaveDataSpaceId sourceSpaceId, SaveDataSpaceId destinationSpaceId, NativeHandle workBufferHandle,
            ulong workBufferSize)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure())
            {
                saveMover = default;
                return rc;
            }

            return saveFsService.OpenSaveDataMover(out saveMover, sourceSpaceId, destinationSpaceId, workBufferHandle,
                workBufferSize);
        }

        public Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize)
        {
            return Result.Success;
        }

        public Result SetSaveDataRootPath(in FspPath path)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.SetSaveDataRootPath(in path);
        }

        public Result UnsetSaveDataRootPath()
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.UnsetSaveDataRootPath();
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

        public Result OpenCustomStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, CustomStorageId storageId)
        {
            fileSystem = default;
            var storageFlag = StorageType.NonGameCard;
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            AccessibilityType accessType = storageId > CustomStorageId.SdCard
                ? AccessibilityType.NotMount
                : AccessibilityType.MountCustomStorage;

            Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(accessType);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> customFs = null;
            try
            {
                rc = FsProxyCore.OpenCustomStorageFileSystem(out customFs, storageId);
                if (rc.IsFailure()) return rc;

                customFs = StorageLayoutTypeSetFileSystem.CreateShared(ref customFs, storageFlag);
                customFs = AsynchronousAccessFileSystem.CreateShared(ref customFs);
                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref customFs);

                return Result.Success;
            }
            finally
            {
                customFs?.Dispose();
            }
        }

        public Result OpenGameCardFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            GameCardHandle handle, GameCardPartition partitionId)
        {
            return GetBaseFileSystemService().OpenGameCardFileSystem(out fileSystem, handle, partitionId);
        }

        public Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize)
        {
            Unsafe.SkipInit(out totalSize);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.QuerySaveDataTotalSize(out totalSize, dataSize, journalSize);
        }

        public Result SetCurrentPosixTimeWithTimeDifference(long currentTime, int timeDifference)
        {
            return GetTimeService().SetCurrentPosixTimeWithTimeDifference(currentTime, timeDifference);
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

            rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            rc = saveFsService.SetSdCardEncryptionSeed(in seed);
            if (rc.IsFailure()) return rc;

            rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.SetSdCardEncryptionSeed(in seed);
        }

        public Result RegisterProgramIndexMapInfo(InBuffer programIndexMapInfoBuffer, int programCount)
        {
            return GetProgramIndexRegistryService()
                .RegisterProgramIndexMapInfo(programIndexMapInfoBuffer, programCount);
        }

        public Result SetBisRootForHost(BisPartitionId partitionId, in FspPath path)
        {
            throw new NotImplementedException();
        }

        public Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId,
            OutBuffer readBuffer)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.VerifySaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId, readBuffer);
        }

        public Result VerifySaveDataFileSystem(ulong saveDataId, OutBuffer readBuffer)
        {
            return VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId.System, saveDataId, readBuffer);
        }

        public Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.CorruptSaveDataFileSystemByOffset(spaceId, saveDataId, offset);
        }

        public Result CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
        {
            // Corrupt both of the save data headers
            Result rc = CorruptSaveDataFileSystemByOffset(spaceId, saveDataId, 0);
            if (rc.IsFailure()) return rc;

            return CorruptSaveDataFileSystemByOffset(spaceId, saveDataId, 0x4000);
        }

        public Result CorruptSaveDataFileSystem(ulong saveDataId)
        {
            return CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId.System, saveDataId);
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
            return Result.Success;
        }

        public Result SetGlobalAccessLogMode(GlobalAccessLogMode mode)
        {
            return GetAccessLogService().SetAccessLogMode(mode);

        }

        public Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode)
        {
            return GetAccessLogService().GetAccessLogMode(out mode);
        }

        public Result GetProgramIndexForAccessLog(out int programIndex, out int programCount)
        {
            return GetProgramIndexRegistryService().GetProgramIndex(out programIndex, out programCount);
        }

        public Result OutputAccessLogToSdCard(InBuffer textBuffer)
        {
            return GetAccessLogService().OutputAccessLogToSdCard(textBuffer);
        }

        public Result OutputMultiProgramTagAccessLog()
        {
            return GetAccessLogService().OutputMultiProgramTagAccessLog();
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

        public Result OverrideSaveDataTransferTokenSignVerificationKey(InBuffer key)
        {
            throw new NotImplementedException();
        }

        public Result SetSdCardAccessibility(bool isAccessible)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.SetSdCardAccessibility(isAccessible);
        }

        public Result IsSdCardAccessible(out bool isAccessible)
        {
            Unsafe.SkipInit(out isAccessible);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.IsSdCardAccessible(out isAccessible);
        }

        public Result OpenMultiCommitManager(out ReferenceCountedDisposable<IMultiCommitManager> commitManager)
        {
            commitManager = null;

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OpenMultiCommitManager(out commitManager);
        }

        public Result OpenBisWiper(out ReferenceCountedDisposable<IWiper> bisWiper, NativeHandle transferMemoryHandle,
            ulong transferMemorySize)
        {
            return GetBaseFileSystemService().OpenBisWiper(out bisWiper, transferMemoryHandle, transferMemorySize);
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            return FsProxyCore.ProgramRegistry.GetProgramInfo(out programInfo, CurrentProcess);
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

        private Result GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService)
        {
            if (SaveFsService is null)
            {
                saveFsService = null;
                return ResultFs.PreconditionViolation.Log();
            }

            saveFsService = SaveFsService.Target;
            return Result.Success;
        }

        private BaseStorageService GetBaseStorageService()
        {
            return new BaseStorageService(FsProxyCore.Config.BaseStorageService, CurrentProcess);
        }

        private BaseFileSystemService GetBaseFileSystemService()
        {
            return new BaseFileSystemService(FsProxyCore.Config.BaseFileSystemService, CurrentProcess);
        }

        private TimeService GetTimeService()
        {
            return new TimeService(FsProxyCore.Config.TimeService, CurrentProcess);
        }

        private ProgramIndexRegistryService GetProgramIndexRegistryService()
        {
            return new ProgramIndexRegistryService(FsProxyCore.Config.ProgramRegistryService, CurrentProcess);
        }

        private AccessLogService GetAccessLogService()
        {
            return new AccessLogService(FsProxyCore.Config.AccessLogService, CurrentProcess);
        }
    }
}
