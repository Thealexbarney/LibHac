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
using Path = LibHac.Fs.Path;
using static LibHac.Fs.StringTraits;

namespace LibHac.FsSrv;

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
        g.DebugConfigurationServiceImpl = configuration.DebugConfigurationService;
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
    public DebugConfigurationServiceImpl DebugConfigurationServiceImpl;
    public Optional<FileSystemProxyCoreImpl> FileSystemProxyCoreImpl;
}

/// <summary>
/// Dispatches calls to the various file system service objects.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public class FileSystemProxyImpl : IFileSystemProxy, IFileSystemProxyForLoader
{
    private FileSystemProxyCoreImpl _fsProxyCore;
    private SharedRef<NcaFileSystemService> _ncaFsService;
    private SharedRef<SaveDataFileSystemService> _saveFsService;
    private ulong _currentProcess;

    // LibHac addition
    private FileSystemServer _fsServer;
    private ref FileSystemProxyImplGlobals Globals => ref _fsServer.Globals.FileSystemProxyImpl;

    internal FileSystemProxyImpl(FileSystemServer server)
    {
        _fsServer = server;

        _fsProxyCore = Globals.FileSystemProxyCoreImpl.Value;
        _currentProcess = ulong.MaxValue;
    }

    public void Dispose()
    {
        _ncaFsService.Destroy();
        _saveFsService.Destroy();
    }

    private Result GetProgramInfo(out ProgramInfo programInfo)
    {
        var registry = new ProgramRegistryImpl(_fsServer);
        return registry.GetProgramInfo(out programInfo, _currentProcess);
    }

    private Result GetNcaFileSystemService(out NcaFileSystemService ncaFsService)
    {
        if (!_ncaFsService.HasValue)
        {
            UnsafeHelpers.SkipParamInit(out ncaFsService);
            return ResultFs.PreconditionViolation.Log();
        }

        ncaFsService = _ncaFsService.Get;
        return Result.Success;
    }

    private Result GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService)
    {
        if (!_saveFsService.HasValue)
        {
            UnsafeHelpers.SkipParamInit(out saveFsService);
            return ResultFs.PreconditionViolation.Log();
        }

        saveFsService = _saveFsService.Get;
        return Result.Success;
    }

    private BaseStorageService GetBaseStorageService()
    {
        return new BaseStorageService(Globals.BaseStorageServiceImpl, _currentProcess);
    }

    private BaseFileSystemService GetBaseFileSystemService()
    {
        return new BaseFileSystemService(Globals.BaseFileSystemServiceImpl, _currentProcess);
    }

    private AccessFailureManagementService GetAccessFailureManagementService()
    {
        return new AccessFailureManagementService(Globals.AccessFailureManagementServiceImpl, _currentProcess);
    }

    private TimeService GetTimeService()
    {
        return new TimeService(Globals.TimeServiceImpl, _currentProcess);
    }

    private StatusReportService GetStatusReportService()
    {
        return new StatusReportService(Globals.StatusReportServiceImpl);
    }

    private ProgramIndexRegistryService GetProgramIndexRegistryService()
    {
        return new ProgramIndexRegistryService(_fsServer, Globals.ProgramRegistryServiceImpl, _currentProcess);
    }

    private AccessLogService GetAccessLogService()
    {
        return new AccessLogService(Globals.AccessLogServiceImpl, _currentProcess);
    }

    private DebugConfigurationService GetDebugConfigurationService()
    {
        return new DebugConfigurationService(_fsServer, Globals.DebugConfigurationServiceImpl, _currentProcess);
    }

    public Result OpenFileSystemWithId(ref SharedRef<IFileSystemSf> outFileSystem, ref readonly FspPath path,
        ulong id, FileSystemProxyType fsType)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenFileSystemWithId(ref outFileSystem, in path, id, fsType);
    }

    public Result OpenFileSystemWithPatch(ref SharedRef<IFileSystemSf> outFileSystem,
        ProgramId programId, FileSystemProxyType fsType)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenFileSystemWithPatch(ref outFileSystem, programId, fsType);
    }

    public Result OpenCodeFileSystem(ref SharedRef<IFileSystemSf> fileSystem,
        out CodeVerificationData verificationData, ref readonly FspPath path, ProgramId programId)
    {
        UnsafeHelpers.SkipParamInit(out verificationData);

        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenCodeFileSystem(ref fileSystem, out verificationData, in path, programId);
    }

    public Result SetCurrentProcess(ulong processId)
    {
        _currentProcess = processId;

        // Initialize the NCA file system service
        using SharedRef<NcaFileSystemService> ncaFsService =
            NcaFileSystemService.CreateShared(Globals.NcaFileSystemServiceImpl, processId);
        _ncaFsService.SetByMove(ref ncaFsService.Ref);

        using SharedRef<SaveDataFileSystemService> saveFsService =
            SaveDataFileSystemService.CreateShared(Globals.SaveDataFileSystemServiceImpl, processId);
        _saveFsService.SetByMove(ref saveFsService.Ref);

        return Result.Success;
    }

    public Result GetFreeSpaceSizeForSaveData(out long freeSpaceSize, SaveDataSpaceId spaceId)
    {
        UnsafeHelpers.SkipParamInit(out freeSpaceSize);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.GetFreeSpaceSizeForSaveData(out freeSpaceSize, spaceId);
    }

    public Result OpenDataFileSystemByCurrentProcess(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataFileSystemByCurrentProcess(ref outFileSystem);
    }

    public Result OpenDataFileSystemByProgramId(ref SharedRef<IFileSystemSf> outFileSystem,
        ProgramId programId)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataFileSystemByProgramId(ref outFileSystem, programId);
    }

    public Result OpenDataStorageByCurrentProcess(ref SharedRef<IStorageSf> outStorage)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataStorageByCurrentProcess(ref outStorage);
    }

    public Result OpenDataStorageByProgramId(ref SharedRef<IStorageSf> outStorage,
        ProgramId programId)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataStorageByProgramId(ref outStorage, programId);
    }

    public Result OpenDataStorageByDataId(ref SharedRef<IStorageSf> outStorage, DataId dataId,
        StorageId storageId)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataStorageByDataId(ref outStorage, dataId, storageId);
    }

    public Result OpenDataStorageByPath(ref SharedRef<IFileSystemSf> outFileSystem, ref readonly FspPath path,
        FileSystemProxyType fsType)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataStorageByPath(ref outFileSystem, in path, fsType);
    }

    public Result OpenPatchDataStorageByCurrentProcess(ref SharedRef<IStorageSf> outStorage)
    {
        return ResultFs.TargetNotFound.Log();
    }

    public Result OpenDataFileSystemWithProgramIndex(ref SharedRef<IFileSystemSf> outFileSystem,
        byte programIndex)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataFileSystemWithProgramIndex(ref outFileSystem, programIndex);
    }

    public Result OpenDataStorageWithProgramIndex(ref SharedRef<IStorageSf> outStorage,
        byte programIndex)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenDataStorageWithProgramIndex(ref outStorage, programIndex);
    }

    public Result RegisterSaveDataFileSystemAtomicDeletion(InBuffer saveDataIds)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.RegisterSaveDataFileSystemAtomicDeletion(saveDataIds);
    }

    public Result DeleteSaveDataFileSystem(ulong saveDataId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.DeleteSaveDataFileSystem(saveDataId);
    }

    public Result DeleteSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.DeleteSaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId);
    }

    public Result DeleteSaveDataFileSystemBySaveDataAttribute(SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.DeleteSaveDataFileSystemBySaveDataAttribute(spaceId, in attribute);
    }

    public Result UpdateSaveDataMacForDebug(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.UpdateSaveDataMacForDebug(spaceId, saveDataId);
    }

    public Result CreateSaveDataFileSystem(in SaveDataAttribute attribute, in SaveDataCreationInfo creationInfo,
        in SaveDataMetaInfo metaInfo)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.CreateSaveDataFileSystem(in attribute, in creationInfo, in metaInfo);
    }

    public Result CreateSaveDataFileSystemWithHashSalt(in SaveDataAttribute attribute,
        in SaveDataCreationInfo creationInfo, in SaveDataMetaInfo metaInfo, in HashSalt hashSalt)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.CreateSaveDataFileSystemWithHashSalt(in attribute, in creationInfo, in metaInfo,
            in hashSalt);
    }

    public Result CreateSaveDataFileSystemBySystemSaveDataId(in SaveDataAttribute attribute,
        in SaveDataCreationInfo creationInfo)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.CreateSaveDataFileSystemBySystemSaveDataId(in attribute, in creationInfo);
    }

    public Result CreateSaveDataFileSystemWithCreationInfo2(in SaveDataCreationInfo2 creationInfo)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.CreateSaveDataFileSystemWithCreationInfo2(in creationInfo);
    }

    public Result ExtendSaveDataFileSystem(SaveDataSpaceId spaceId, ulong saveDataId, long dataSize,
        long journalSize)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.ExtendSaveDataFileSystem(spaceId, saveDataId, dataSize, journalSize);
    }

    public Result OpenSaveDataFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataFileSystem(ref outFileSystem, spaceId, in attribute);
    }

    public Result OpenReadOnlySaveDataFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenReadOnlySaveDataFileSystem(ref outFileSystem, spaceId, in attribute);
    }

    public Result OpenSaveDataFileSystemBySystemSaveDataId(ref SharedRef<IFileSystemSf> outFileSystem,
        SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataFileSystemBySystemSaveDataId(ref outFileSystem, spaceId, in attribute);
    }

    public Result ReadSaveDataFileSystemExtraData(OutBuffer extraDataBuffer, ulong saveDataId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.ReadSaveDataFileSystemExtraData(extraDataBuffer, saveDataId);
    }

    public Result ReadSaveDataFileSystemExtraDataBySaveDataAttribute(OutBuffer extraDataBuffer,
        SaveDataSpaceId spaceId, in SaveDataAttribute attribute)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.ReadSaveDataFileSystemExtraDataBySaveDataAttribute(extraDataBuffer, spaceId,
            in attribute);
    }

    public Result ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(OutBuffer extraDataBuffer,
        SaveDataSpaceId spaceId, in SaveDataAttribute attribute, InBuffer maskBuffer)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.ReadSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(extraDataBuffer, spaceId,
            in attribute, maskBuffer);
    }

    public Result ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(OutBuffer extraDataBuffer,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.ReadSaveDataFileSystemExtraDataBySaveDataSpaceId(extraDataBuffer, spaceId, saveDataId);
    }

    public Result WriteSaveDataFileSystemExtraData(ulong saveDataId, SaveDataSpaceId spaceId,
        InBuffer extraDataBuffer)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.WriteSaveDataFileSystemExtraData(saveDataId, spaceId, extraDataBuffer);
    }

    public Result WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in SaveDataAttribute attribute,
        SaveDataSpaceId spaceId, InBuffer extraDataBuffer, InBuffer maskBuffer)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.WriteSaveDataFileSystemExtraDataWithMaskBySaveDataAttribute(in attribute, spaceId,
            extraDataBuffer, maskBuffer);
    }

    public Result WriteSaveDataFileSystemExtraDataWithMask(ulong saveDataId, SaveDataSpaceId spaceId,
        InBuffer extraDataBuffer, InBuffer maskBuffer)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.WriteSaveDataFileSystemExtraDataWithMask(saveDataId, spaceId, extraDataBuffer,
            maskBuffer);
    }

    public Result OpenImageDirectoryFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        ImageDirectoryId directoryId)
    {
        return GetBaseFileSystemService().OpenImageDirectoryFileSystem(ref outFileSystem, directoryId);
    }

    public Result OpenBaseFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, BaseFileSystemId fileSystemId)
    {
        return GetBaseFileSystemService().OpenBaseFileSystem(ref outFileSystem, fileSystemId);
    }

    public Result FormatBaseFileSystem(BaseFileSystemId fileSystemId)
    {
        return GetBaseFileSystemService().FormatBaseFileSystem(fileSystemId);
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, ref readonly FspPath rootPath,
        BisPartitionId partitionId)
    {
        return GetBaseFileSystemService().OpenBisFileSystem(ref outFileSystem, in rootPath, partitionId);
    }

    public Result OpenBisStorage(ref SharedRef<IStorageSf> outStorage, BisPartitionId partitionId)
    {
        return GetBaseStorageService().OpenBisStorage(ref outStorage, partitionId);
    }

    public Result InvalidateBisCache()
    {
        return GetBaseStorageService().InvalidateBisCache();
    }

    public Result OpenHostFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, ref readonly FspPath path)
    {
        return OpenHostFileSystemWithOption(ref outFileSystem, in path, MountHostOption.None);
    }

    public Result OpenHostFileSystemWithOption(ref SharedRef<IFileSystemSf> outFileSystem,
        ref readonly FspPath path, MountHostOption option)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountHost);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        using var hostFileSystem = new SharedRef<IFileSystem>();
        using var pathNormalized = new Path();

        if (path.Str.At(0) == DirectorySeparator && path.Str.At(1) != DirectorySeparator)
        {
            res = pathNormalized.Initialize(path.Str.Slice(1));
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = pathNormalized.InitializeWithReplaceUnc(path.Str);
            if (res.IsFailure()) return res.Miss();
        }

        var flags = new PathFlags();
        flags.AllowWindowsPath();
        flags.AllowRelativePath();
        flags.AllowEmptyPath();

        res = pathNormalized.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        bool isCaseSensitive = option.Flags.HasFlag(MountHostOptionFlag.PseudoCaseSensitive);

        res = _fsProxyCore.OpenHostFileSystem(ref hostFileSystem.Ref, in pathNormalized, isCaseSensitive);
        if (res.IsFailure()) return res.Miss();

        var adapterFlags = new PathFlags();
        if (path.Str.At(0) == NullTerminator)
            adapterFlags.AllowWindowsPath();

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref hostFileSystem.Ref, adapterFlags, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenSdCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        return GetBaseFileSystemService().OpenSdCardFileSystem(ref outFileSystem);
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

    public Result OpenGameCardStorage(ref SharedRef<IStorageSf> outStorage, GameCardHandle handle,
        GameCardPartitionRaw partitionId)
    {
        return GetBaseStorageService().OpenGameCardStorage(ref outStorage, handle, partitionId);
    }

    public Result OpenDeviceOperator(ref SharedRef<IDeviceOperator> outDeviceOperator)
    {
        return GetBaseStorageService().OpenDeviceOperator(ref outDeviceOperator);
    }

    public Result OpenSdCardDetectionEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier)
    {
        return GetBaseStorageService().OpenSdCardDetectionEventNotifier(ref outEventNotifier);
    }

    public Result OpenGameCardDetectionEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier)
    {
        return GetBaseStorageService().OpenGameCardDetectionEventNotifier(ref outEventNotifier);
    }

    public Result SimulateDeviceDetectionEvent(SdmmcPort port, SimulatingDeviceDetectionMode mode, bool signalEvent)
    {
        return GetBaseStorageService().SimulateDeviceDetectionEvent(port, mode, signalEvent);
    }

    public Result OpenSystemDataUpdateEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenSystemDataUpdateEventNotifier(ref outEventNotifier);
    }

    public Result NotifySystemDataUpdateEvent()
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.NotifySystemDataUpdateEvent();
    }

    public Result OpenSaveDataInfoReader(ref SharedRef<ISaveDataInfoReader> outInfoReader)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataInfoReader(ref outInfoReader);
    }

    public Result OpenSaveDataInfoReaderBySaveDataSpaceId(
        ref SharedRef<ISaveDataInfoReader> outInfoReader, SaveDataSpaceId spaceId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataInfoReaderBySaveDataSpaceId(ref outInfoReader, spaceId);
    }

    public Result OpenSaveDataInfoReaderWithFilter(ref SharedRef<ISaveDataInfoReader> outInfoReader,
        SaveDataSpaceId spaceId, in SaveDataFilter filter)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataInfoReaderWithFilter(ref outInfoReader, spaceId, in filter);
    }

    public Result FindSaveDataWithFilter(out long count, OutBuffer saveDataInfoBuffer, SaveDataSpaceId spaceId,
        in SaveDataFilter filter)
    {
        UnsafeHelpers.SkipParamInit(out count);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.FindSaveDataWithFilter(out count, saveDataInfoBuffer, spaceId, in filter);
    }

    public Result OpenSaveDataInternalStorageFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        SaveDataSpaceId spaceId, ulong saveDataId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataInternalStorageFileSystem(ref outFileSystem, spaceId, saveDataId);
    }

    public Result QuerySaveDataInternalStorageTotalSize(out long size, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.QuerySaveDataInternalStorageTotalSize(out size, spaceId, saveDataId);
    }

    public Result GetSaveDataCommitId(out long commitId, SaveDataSpaceId spaceId, ulong saveDataId)
    {
        UnsafeHelpers.SkipParamInit(out commitId);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.GetSaveDataCommitId(out commitId, spaceId, saveDataId);
    }

    public Result OpenSaveDataInfoReaderOnlyCacheStorage(ref SharedRef<ISaveDataInfoReader> outInfoReader)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataInfoReaderOnlyCacheStorage(ref outInfoReader);
    }

    public Result OpenSaveDataMetaFile(ref SharedRef<IFileSf> outFile, SaveDataSpaceId spaceId,
        in SaveDataAttribute attribute, SaveDataMetaType type)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataMetaFile(ref outFile, spaceId, in attribute, type);
    }

    public Result DeleteCacheStorage(ushort index)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.DeleteCacheStorage(index);
    }

    public Result GetCacheStorageSize(out long dataSize, out long journalSize, ushort index)
    {
        UnsafeHelpers.SkipParamInit(out dataSize, out journalSize);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.GetCacheStorageSize(out dataSize, out journalSize, index);
    }

    public Result OpenSaveDataTransferManager(ref SharedRef<ISaveDataTransferManager> outTransferManager)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataTransferManager(ref outTransferManager);
    }

    public Result OpenSaveDataTransferManagerVersion2(
        ref SharedRef<ISaveDataTransferManagerWithDivision> outTransferManager)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataTransferManagerVersion2(ref outTransferManager);
    }

    public Result OpenSaveDataTransferManagerForSaveDataRepair(
        ref SharedRef<ISaveDataTransferManagerForSaveDataRepair> outTransferManager)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataTransferManagerForSaveDataRepair(ref outTransferManager);
    }

    public Result OpenSaveDataTransferManagerForRepair(
        ref SharedRef<ISaveDataTransferManagerForRepair> outTransferManager)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataTransferManagerForRepair(ref outTransferManager);
    }

    public Result OpenSaveDataTransferProhibiter(ref SharedRef<ISaveDataTransferProhibiter> outProhibiter,
        Ncm.ApplicationId applicationId)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataTransferProhibiter(ref outProhibiter, applicationId);
    }

    public Result ListAccessibleSaveDataOwnerId(out int readCount, OutBuffer idBuffer, ProgramId programId,
        int startIndex, int bufferIdCount)
    {
        UnsafeHelpers.SkipParamInit(out readCount);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.ListAccessibleSaveDataOwnerId(out readCount, idBuffer, programId, startIndex,
            bufferIdCount);
    }

    public Result OpenSaveDataMover(ref SharedRef<ISaveDataMover> outSaveDataMover, SaveDataSpaceId sourceSpaceId,
        SaveDataSpaceId destinationSpaceId, NativeHandle workBufferHandle, ulong workBufferSize)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenSaveDataMover(ref outSaveDataMover, sourceSpaceId, destinationSpaceId,
            workBufferHandle, workBufferSize);
    }

    public Result SetSaveDataSize(long saveDataSize, long saveDataJournalSize)
    {
        return Result.Success;
    }

    public Result SetSaveDataRootPath(ref readonly FspPath path)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.SetSaveDataRootPath(in path);
    }

    public Result UnsetSaveDataRootPath()
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.UnsetSaveDataRootPath();
    }

    public Result OpenContentStorageFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        ContentStorageId storageId)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenContentStorageFileSystem(ref outFileSystem, storageId);
    }

    public Result OpenCloudBackupWorkStorageFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        CloudBackupWorkStorageId storageId)
    {
        var storageFlag = StorageLayoutType.NonGameCard;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        Accessibility accessibility =
            programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountCloudBackupWorkStorage);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _fsProxyCore.OpenCloudBackupWorkStorageFileSystem(ref fileSystem.Ref, storageId);
        if (res.IsFailure()) return res.Miss();

        // Add all the wrappers for the file system
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref, storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenCustomStorageFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, CustomStorageId storageId)
    {
        var storageFlag = StorageLayoutType.NonGameCard;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        AccessibilityType accessType = storageId > CustomStorageId.SdCard
            ? AccessibilityType.NotMount
            : AccessibilityType.MountCustomStorage;

        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(accessType);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _fsProxyCore.OpenCustomStorageFileSystem(ref fileSystem.Ref, storageId);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref, storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenGameCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        return GetBaseFileSystemService().OpenGameCardFileSystem(ref outFileSystem, handle, partitionId);
    }

    public Result IsArchivedProgram(out bool isArchived, ulong processId)
    {
        UnsafeHelpers.SkipParamInit(out isArchived);

        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.IsArchivedProgram(out isArchived, processId);
    }

    public Result QuerySaveDataTotalSize(out long totalSize, long dataSize, long journalSize)
    {
        UnsafeHelpers.SkipParamInit(out totalSize);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.QuerySaveDataTotalSize(out totalSize, dataSize, journalSize);
    }

    public Result SetCurrentPosixTimeWithTimeDifference(long currentTime, int timeDifference)
    {
        return GetTimeService().SetCurrentPosixTimeWithTimeDifference(currentTime, timeDifference);
    }

    public Result GetRightsId(out RightsId rightsId, ProgramId programId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out rightsId);

        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.GetRightsId(out rightsId, programId, storageId);
    }

    public Result GetRightsIdByPath(out RightsId rightsId, ref readonly FspPath path)
    {
        return GetRightsIdAndKeyGenerationByPath(out rightsId, out _, in path);
    }

    public Result GetRightsIdAndKeyGenerationByPath(out RightsId rightsId, out byte keyGeneration,
        ref readonly FspPath path)
    {
        UnsafeHelpers.SkipParamInit(out rightsId, out keyGeneration);

        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, in path);
    }

    public Result RegisterExternalKey(in RightsId rightsId, in AccessKey externalKey)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.RegisterExternalKey(in rightsId, in externalKey);
    }

    public Result UnregisterExternalKey(in RightsId rightsId)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.UnregisterExternalKey(in rightsId);
    }

    public Result UnregisterAllExternalKey()
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.UnregisterAllExternalKey();
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed seed)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.SetEncryptionSeed))
            return ResultFs.PermissionDenied.Log();

        res = _fsProxyCore.SetSdCardEncryptionSeed(in seed);
        if (res.IsFailure()) return res.Miss();

        res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        res = saveFsService.SetSdCardEncryptionSeed(in seed);
        if (res.IsFailure()) return res.Miss();

        res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

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

    public Result SetBisRootForHost(BisPartitionId partitionId, ref readonly FspPath path)
    {
        return GetBaseFileSystemService().SetBisRootForHost(partitionId, in path);
    }

    public Result VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId,
        OutBuffer readBuffer)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.VerifySaveDataFileSystemBySaveDataSpaceId(spaceId, saveDataId, readBuffer);
    }

    public Result VerifySaveDataFileSystem(ulong saveDataId, OutBuffer readBuffer)
    {
        return VerifySaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId.System, saveDataId, readBuffer);
    }

    public Result CorruptSaveDataFileSystemByOffset(SaveDataSpaceId spaceId, ulong saveDataId, long offset)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.CorruptSaveDataFileSystemByOffset(spaceId, saveDataId, offset);
    }

    public Result CorruptSaveDataFileSystemBySaveDataSpaceId(SaveDataSpaceId spaceId, ulong saveDataId)
    {
        // Corrupt both of the save data headers
        Result res = CorruptSaveDataFileSystemByOffset(spaceId, saveDataId, 0);
        if (res.IsFailure()) return res.Miss();

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
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.RegisterUpdatePartition();
    }

    public Result OpenRegisteredUpdatePartition(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        Result res = GetNcaFileSystemService(out NcaFileSystemService ncaFsService);
        if (res.IsFailure()) return res.Miss();

        return ncaFsService.OpenRegisteredUpdatePartition(ref outFileSystem);
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
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OverrideSaveDataTransferTokenSignVerificationKey(key);
    }

    public Result SetSdCardAccessibility(bool isAccessible)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.SetSdCardAccessibility(isAccessible);
    }

    public Result IsSdCardAccessible(out bool isAccessible)
    {
        UnsafeHelpers.SkipParamInit(out isAccessible);

        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.IsSdCardAccessible(out isAccessible);
    }

    public Result OpenAccessFailureDetectionEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier,
        ulong processId, bool notifyOnDeepRetry)
    {
        return GetAccessFailureManagementService()
            .OpenAccessFailureDetectionEventNotifier(ref outEventNotifier, processId, notifyOnDeepRetry);
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

    public Result OpenMultiCommitManager(ref SharedRef<IMultiCommitManager> outCommitManager)
    {
        Result res = GetSaveDataFileSystemService(out SaveDataFileSystemService saveFsService);
        if (res.IsFailure()) return res.Miss();

        return saveFsService.OpenMultiCommitManager(ref outCommitManager);
    }

    public Result OpenBisWiper(ref SharedRef<IWiper> outBisWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        return GetBaseFileSystemService().OpenBisWiper(ref outBisWiper, transferMemoryHandle, transferMemorySize);
    }

    public Result RegisterDebugConfiguration(uint key, long value)
    {
        return GetDebugConfigurationService().Register(key, value);
    }

    public Result UnregisterDebugConfiguration(uint key)
    {
        return GetDebugConfigurationService().Unregister(key);
    }
}