using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Spl;
using LibHac.Util;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IFileSf = LibHac.FsSrv.Sf.IFile;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv
{
    public static class FileSystemProxyImplGlobalMethods
    {
        public static void InitializeFileSystemProxy(this FileSystemServer fsSrv,
            FileSystemProxyConfiguration configuration)
        {
            ref FileSystemProxyImplGlobals g = ref fsSrv.Globals.FileSystemProxyImpl;

            g.FileSystemProxyCoreImpl.Set(new FileSystemProxyCoreImpl(configuration.FsCreatorInterfaces,
                configuration.BaseFileSystemService));

            g.BaseStorageServiceImpl = configuration.BaseStorageService;
            g.BaseFileSystemServiceImpl = configuration.BaseFileSystemService;
            g.NcaFileSystemServiceImpl = configuration.NcaFileSystemService;
            g.SaveDataFileSystemServiceImpl = configuration.SaveDataFileSystemService;
            g.AccessFailureManagementServiceImpl = configuration.AccessFailureManagementService;
            g.TimeServiceImpl = configuration.TimeService;
            g.StatusReportServiceImpl = configuration.StatusReportService;
            g.ProgramRegistryServiceImpl = configuration.ProgramRegistryService;
            g.AccessLogServiceImpl = configuration.AccessLogService;
        }
    }

    internal struct FileSystemProxyImplGlobals
    {
        public NcaFileSystemServiceImpl NcaFileSystemServiceImpl;
        public SaveDataFileSystemServiceImpl SaveDataFileSystemServiceImpl;
        public BaseStorageServiceImpl BaseStorageServiceImpl;
        public BaseFileSystemServiceImpl BaseFileSystemServiceImpl;
        public AccessFailureManagementServiceImpl AccessFailureManagementServiceImpl;
        public TimeServiceImpl TimeServiceImpl;
        public StatusReportServiceImpl StatusReportServiceImpl;
        public ProgramRegistryServiceImpl ProgramRegistryServiceImpl;
        public AccessLogServiceImpl AccessLogServiceImpl;
        public Optional<FileSystemProxyCoreImpl> FileSystemProxyCoreImpl;
    }

    public class FileSystemProxyImpl : IFileSystemProxy, IFileSystemProxyForLoader
    {
        private FileSystemServer FsServer { get; }
        private ref FileSystemProxyImplGlobals Globals => ref FsServer.Globals.FileSystemProxyImpl;

        private FileSystemProxyCoreImpl FsProxyCore { get; }
        private ReferenceCountedDisposable<NcaFileSystemService> NcaFsService { get; set; }
        private ReferenceCountedDisposable<SaveDataFileSystemService> SaveFsService { get; set; }
        private ulong CurrentProcess { get; set; }

        internal FileSystemProxyImpl(FileSystemServer server)
        {
            FsServer = server;

            FsProxyCore = Globals.FileSystemProxyCoreImpl.Value;
            CurrentProcess = ulong.MaxValue;
        }

        public void Dispose()
        {
            NcaFsService?.Dispose();
            SaveFsService?.Dispose();
        }

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            var registry = new ProgramRegistryImpl(FsServer);
            return registry.GetProgramInfo(out programInfo, CurrentProcess);
        }

        private Result GetNcaFileSystemService(out NcaFileSystemService ncaFsService)
        {
            if (NcaFsService is null)
            {
                UnsafeHelpers.SkipParamInit(out ncaFsService);
                return ResultFs.PreconditionViolation.Log();
            }

            ncaFsService = NcaFsService.Target;
            return Result.Success;
        }

        private Result GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService)
        {
            if (SaveFsService is null)
            {
                UnsafeHelpers.SkipParamInit(out saveFsService);
                return ResultFs.PreconditionViolation.Log();
            }

            saveFsService = SaveFsService.Target;
            return Result.Success;
        }

        private BaseStorageService GetBaseStorageService()
        {
            return new BaseStorageService(Globals.BaseStorageServiceImpl, CurrentProcess);
        }

        private BaseFileSystemService GetBaseFileSystemService()
        {
            return new BaseFileSystemService(Globals.BaseFileSystemServiceImpl, CurrentProcess);
        }

        private AccessFailureManagementService GetAccessFailureManagementService()
        {
            return new AccessFailureManagementService(Globals.AccessFailureManagementServiceImpl, CurrentProcess);
        }

        private TimeService GetTimeService()
        {
            return new TimeService(Globals.TimeServiceImpl, CurrentProcess);
        }

        private StatusReportService GetStatusReportService()
        {
            return new StatusReportService(Globals.StatusReportServiceImpl);
        }

        private ProgramIndexRegistryService GetProgramIndexRegistryService()
        {
            return new ProgramIndexRegistryService(Globals.ProgramRegistryServiceImpl, CurrentProcess);
        }

        private AccessLogService GetAccessLogService()
        {
            return new AccessLogService(Globals.AccessLogServiceImpl, CurrentProcess);
        }

        public Result OpenFileSystemWithId(out ReferenceCountedDisposable<IFileSystemSf> fileSystem, in FspPath path,
            ulong id, FileSystemProxyType fsType)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out fileSystem);
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
                UnsafeHelpers.SkipParamInit(out fileSystem);
                return rc;
            }

            return ncaFsService.OpenFileSystemWithPatch(out fileSystem, programId, fsType);
        }

        public Result OpenCodeFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            out CodeVerificationData verificationData, in FspPath path, ProgramId programId)
        {
            UnsafeHelpers.SkipParamInit(out verificationData);

            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out fileSystem);
                return rc;
            }

            return ncaFsService.OpenCodeFileSystem(out fileSystem, out verificationData, in path, programId);
        }

        public Result SetCurrentProcess(ulong processId)
        {
            CurrentProcess = processId;

            // Initialize the NCA file system service
            NcaFsService = NcaFileSystemService.CreateShared(Globals.NcaFileSystemServiceImpl, processId);
            SaveFsService = SaveDataFileSystemService.CreateShared(Globals.SaveDataFileSystemServiceImpl, processId);

            return Result.Success;
        }

        public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
        {
            UnsafeHelpers.SkipParamInit(out freeSpaceSize);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.GetFreeSpaceSizeForSaveData(out freeSpaceSize, spaceId);
        }

        public Result OpenDataFileSystemByCurrentProcess(out ReferenceCountedDisposable<IFileSystemSf> fileSystem)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out fileSystem);
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
                UnsafeHelpers.SkipParamInit(out fileSystem);
                return rc;
            }

            return ncaFsService.OpenDataFileSystemByProgramId(out fileSystem, programId);
        }

        public Result OpenDataStorageByCurrentProcess(out ReferenceCountedDisposable<IStorageSf> storage)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out storage);
                return rc;
            }

            return ncaFsService.OpenDataStorageByCurrentProcess(out storage);
        }

        public Result OpenDataStorageByProgramId(out ReferenceCountedDisposable<IStorageSf> storage,
            ProgramId programId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out storage);
                return rc;
            }

            return ncaFsService.OpenDataStorageByProgramId(out storage, programId);
        }

        public Result OpenDataStorageByDataId(out ReferenceCountedDisposable<IStorageSf> storage, DataId dataId,
            StorageId storageId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out storage);
                return rc;
            }

            return ncaFsService.OpenDataStorageByDataId(out storage, dataId, storageId);
        }

        public Result OpenPatchDataStorageByCurrentProcess(out ReferenceCountedDisposable<IStorageSf> storage)
        {
            UnsafeHelpers.SkipParamInit(out storage);
            return ResultFs.TargetNotFound.Log();
        }

        public Result OpenDataFileSystemWithProgramIndex(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            byte programIndex)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out fileSystem);
                return rc;
            }

            return ncaFsService.OpenDataFileSystemWithProgramIndex(out fileSystem, programIndex);
        }

        public Result OpenDataStorageWithProgramIndex(out ReferenceCountedDisposable<IStorageSf> storage,
            byte programIndex)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out storage);
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

        public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId,
            in SaveDataAttribute attribute)
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
                UnsafeHelpers.SkipParamInit(out fileSystem);
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
                UnsafeHelpers.SkipParamInit(out fileSystem);
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
                UnsafeHelpers.SkipParamInit(out fileSystem);
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

        public Result OpenBaseFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            BaseFileSystemId fileSystemId)
        {
            return GetBaseFileSystemService().OpenBaseFileSystem(out fileSystem, fileSystemId);
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
            UnsafeHelpers.SkipParamInit(out fileSystem);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountHost);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> hostFs = null;
            try
            {
                rc = FsProxyCore.OpenHostFileSystem(out hostFs, new U8Span(path.Str),
                    option.Flags.HasFlag(MountHostOptionFlag.PseudoCaseSensitive));
                if (rc.IsFailure()) return rc;

                bool isRootPath = path.Str[0] == 0;

                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref hostFs, isRootPath);

                if (fileSystem is null)
                    return ResultFs.AllocationMemoryFailedCreateShared.Log();

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
                UnsafeHelpers.SkipParamInit(out eventNotifier);
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
                UnsafeHelpers.SkipParamInit(out infoReader);
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
                UnsafeHelpers.SkipParamInit(out infoReader);
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
                UnsafeHelpers.SkipParamInit(out infoReader);
                return rc;
            }

            return saveFsService.OpenSaveDataInfoReaderWithFilter(out infoReader, spaceId, in filter);
        }

        public Result FindSaveDataWithFilter(out long count, OutBuffer saveDataInfoBuffer, SaveDataSpaceId spaceId,
            in SaveDataFilter filter)
        {
            UnsafeHelpers.SkipParamInit(out count);

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
                UnsafeHelpers.SkipParamInit(out fileSystem);
                return rc;
            }

            return saveFsService.OpenSaveDataInternalStorageFileSystem(out fileSystem, spaceId, saveDataId);
        }

        public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out size);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
        }

        public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
        {
            UnsafeHelpers.SkipParamInit(out commitId);

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
                UnsafeHelpers.SkipParamInit(out infoReader);
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
                UnsafeHelpers.SkipParamInit(out file);
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
            UnsafeHelpers.SkipParamInit(out dataSize, out journalSize);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.GetCacheStorageSize(out dataSize, out journalSize, index);
        }

        public Result OpenSaveDataTransferManager(out ReferenceCountedDisposable<ISaveDataTransferManager> manager)
        {
            UnsafeHelpers.SkipParamInit(out manager);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OpenSaveDataTransferManager(out manager);
        }

        public Result OpenSaveDataTransferManagerVersion2(
            out ReferenceCountedDisposable<ISaveDataTransferManagerWithDivision> manager)
        {
            UnsafeHelpers.SkipParamInit(out manager);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OpenSaveDataTransferManagerVersion2(out manager);
        }

        public Result OpenSaveDataTransferManagerForSaveDataRepair(
            out ReferenceCountedDisposable<ISaveDataTransferManagerForSaveDataRepair> manager)
        {
            UnsafeHelpers.SkipParamInit(out manager);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OpenSaveDataTransferManagerForSaveDataRepair(out manager);
        }

        public Result OpenSaveDataTransferManagerForRepair(
            out ReferenceCountedDisposable<ISaveDataTransferManagerForRepair> manager)
        {
            UnsafeHelpers.SkipParamInit(out manager);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OpenSaveDataTransferManagerForRepair(out manager);
        }

        public Result OpenSaveDataTransferProhibiter(
            out ReferenceCountedDisposable<ISaveDataTransferProhibiter> prohibiter, Ncm.ApplicationId applicationId)
        {
            UnsafeHelpers.SkipParamInit(out prohibiter);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OpenSaveDataTransferProhibiter(out prohibiter, applicationId);
        }

        public Result ListAccessibleSaveDataOwnerId(out int readCount, OutBuffer idBuffer, ProgramId programId,
            int startIndex, int bufferIdCount)
        {
            UnsafeHelpers.SkipParamInit(out readCount);

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
                UnsafeHelpers.SkipParamInit(out saveMover);
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

        public Result OpenContentStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            ContentStorageId storageId)
        {
            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure())
            {
                UnsafeHelpers.SkipParamInit(out fileSystem);
                return rc;
            }

            return ncaFsService.OpenContentStorageFileSystem(out fileSystem, storageId);
        }

        public Result OpenCloudBackupWorkStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            CloudBackupWorkStorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);
            var storageFlag = StorageType.NonGameCard;
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountCloudBackupWorkStorage);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IFileSystem> tempFs = null;
            try
            {
                rc = FsProxyCore.OpenCloudBackupWorkStorageFileSystem(out tempFs, storageId);
                if (rc.IsFailure()) return rc;

                tempFs = StorageLayoutTypeSetFileSystem.CreateShared(ref tempFs, storageFlag);
                tempFs = AsynchronousAccessFileSystem.CreateShared(ref tempFs);
                fileSystem = FileSystemInterfaceAdapter.CreateShared(ref tempFs);

                return Result.Success;
            }
            finally
            {
                tempFs?.Dispose();
            }
        }

        public Result OpenCustomStorageFileSystem(out ReferenceCountedDisposable<IFileSystemSf> fileSystem,
            CustomStorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out fileSystem);
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

        public Result IsArchivedProgram(out bool isArchived, ulong processId)
        {
            UnsafeHelpers.SkipParamInit(out isArchived);

            Result rc = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
            if (rc.IsFailure()) return rc;

            return ncaFsService.IsArchivedProgram(out isArchived, processId);
        }

        public Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize)
        {
            UnsafeHelpers.SkipParamInit(out totalSize);

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
            UnsafeHelpers.SkipParamInit(out rightsId);

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
            UnsafeHelpers.SkipParamInit(out rightsId, out keyGeneration);

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

        public Result GetAndClearErrorInfo(out FileSystemProxyErrorInfo errorInfo)
        {
            return GetStatusReportService().GetAndClearFileSystemProxyErrorInfo(out errorInfo);
        }

        public Result RegisterProgramIndexMapInfo(InBuffer programIndexMapInfoBuffer, int programCount)
        {
            return GetProgramIndexRegistryService()
                .RegisterProgramIndexMapInfo(programIndexMapInfoBuffer, programCount);
        }

        public Result SetBisRootForHost(BisPartitionId partitionId, in FspPath path)
        {
            return GetBaseFileSystemService().SetBisRootForHost(partitionId, in path);
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

        public Result OutputApplicationInfoAccessLog(in ApplicationInfo applicationInfo)
        {
            return GetAccessLogService().OutputApplicationInfoAccessLog(in applicationInfo);
        }

        public Result FlushAccessLogOnSdCard()
        {
            return GetAccessLogService().FlushAccessLogOnSdCard();
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
                UnsafeHelpers.SkipParamInit(out fileSystem);
                return rc;
            }

            return ncaFsService.OpenRegisteredUpdatePartition(out fileSystem);
        }

        public Result GetAndClearMemoryReportInfo(out MemoryReportInfo reportInfo)
        {
            return GetStatusReportService().GetAndClearMemoryReportInfo(out reportInfo);
        }

        public Result GetFsStackUsage(out uint stackUsage, FsStackUsageThreadType threadType)
        {
            return GetStatusReportService().GetFsStackUsage(out stackUsage, threadType);
        }

        public Result OverrideSaveDataTransferTokenSignVerificationKey(InBuffer key)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OverrideSaveDataTransferTokenSignVerificationKey(key);
        }

        public Result SetSdCardAccessibility(bool isAccessible)
        {
            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.SetSdCardAccessibility(isAccessible);
        }

        public Result IsSdCardAccessible(out bool isAccessible)
        {
            UnsafeHelpers.SkipParamInit(out isAccessible);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.IsSdCardAccessible(out isAccessible);
        }

        public Result OpenAccessFailureDetectionEventNotifier(out ReferenceCountedDisposable<IEventNotifier> notifier,
            ulong processId, bool notifyOnDeepRetry)
        {
            return GetAccessFailureManagementService()
                .OpenAccessFailureDetectionEventNotifier(out notifier, processId, notifyOnDeepRetry);
        }

        public Result GetAccessFailureDetectionEvent(out NativeHandle eventHandle)
        {
            return GetAccessFailureManagementService().GetAccessFailureDetectionEvent(out eventHandle);
        }

        public Result IsAccessFailureDetected(out bool isDetected, ulong processId)
        {
            return GetAccessFailureManagementService().IsAccessFailureDetected(out isDetected, processId);
        }

        public Result ResolveAccessFailure(ulong processId)
        {
            return GetAccessFailureManagementService().ResolveAccessFailure(processId);
        }

        public Result AbandonAccessFailure(ulong processId)
        {
            return GetAccessFailureManagementService().AbandonAccessFailure(processId);
        }

        public Result OpenMultiCommitManager(out ReferenceCountedDisposable<IMultiCommitManager> commitManager)
        {
            UnsafeHelpers.SkipParamInit(out commitManager);

            Result rc = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
            if (rc.IsFailure()) return rc;

            return saveFsService.OpenMultiCommitManager(out commitManager);
        }

        public Result OpenBisWiper(out ReferenceCountedDisposable<IWiper> bisWiper, NativeHandle transferMemoryHandle,
            ulong transferMemorySize)
        {
            return GetBaseFileSystemService().OpenBisWiper(out bisWiper, transferMemoryHandle, transferMemorySize);
        }
    }
}
