using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSystem;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Spl;
using NcaFsHeader = LibHac.FsSystem.NcaFsHeader;
using RightsId = LibHac.Fs.RightsId;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv;

file static class Anonymous
{
    public static Result GetDeviceHandleByMountName(out GameCardHandle outHandle, U8Span name)
    {
        throw new NotImplementedException();
    }

    public static Result GetGameCardPartitionByMountName(out GameCardPartition outPartition, U8Span name)
    {
        throw new NotImplementedException();
    }

    public static Result GetPartitionIndex(out int outIndex, FileSystemProxyType type)
    {
        switch (type)
        {
            case FileSystemProxyType.Code:
            case FileSystemProxyType.Control:
            case FileSystemProxyType.Manual:
            case FileSystemProxyType.Meta:
            case FileSystemProxyType.Data:
                outIndex = 0;
                return Result.Success;
            case FileSystemProxyType.Rom:
            case FileSystemProxyType.RegisteredUpdate:
                outIndex = 1;
                return Result.Success;
            case FileSystemProxyType.Logo:
                outIndex = 2;
                return Result.Success;
            default:
                UnsafeHelpers.SkipParamInit(out outIndex);
                return ResultFs.InvalidArgument.Log();
        }
    }

    public static void GenerateNcaDigest(out Hash outDigest, NcaReader reader1, NcaReader reader2)
    {
        throw new NotImplementedException();
    }

    public static Result LoadNspdVerificationData(out CodeVerificationData outCodeVerificationData, IFileSystem fileSystem)
    {
        throw new NotImplementedException();
    }
}

public class NcaFileSystemServiceImpl : IDisposable
{
    private Configuration _config;
    private UpdatePartitionPath _updatePartitionPath;
    private ExternalKeyManager _externalKeyManager;
    private SystemDataUpdateEventManager _systemDataUpdateEventManager;
    private EncryptionSeed _encryptionSeed;
    private uint _romFsDeepRetryStartCount;
    private uint _romFsRemountForDataCorruptionCount;
    private uint _romfsUnrecoverableDataCorruptionByRemountCount;
    private uint _romFsRecoveredByInvalidateCacheCount;
    private uint _romFsUnrecoverableByGameCardAccessFailedCount;
    private SdkMutexType _romfsCountMutex;

    public struct Configuration
    {
        public BaseFileSystemServiceImpl BaseFsService;
        public ILocalFileSystemCreator LocalFsCreator;
        public ITargetManagerFileSystemCreator TargetManagerFsCreator;
        public IPartitionFileSystemCreator PartitionFsCreator;
        public IRomFileSystemCreator RomFsCreator;
        public IStorageOnNcaCreator StorageOnNcaCreator;
        public ISubDirectoryFileSystemCreator SubDirectoryFsCreator;
        public IEncryptedFileSystemCreator EncryptedFsCreator;
        public INspRootFileSystemCreator NspRootFileSystemCreator;
        private LocationResolverSet LocationResolverSet;
        public ProgramRegistryServiceImpl ProgramRegistryService;
        public AccessFailureManagementServiceImpl AccessFailureManagementService;
        public InternalProgramIdRangeForSpeedEmulation SpeedEmulationRange;
        public long AddOnContentDivisionSize;
        public long RomDivisionSize;

        // LibHac additions
        public FileSystemServer FsServer;
    }

    private struct MountInfo
    {
        public enum FileSystemType
        {
            None,
            GameCard,
            HostFs,
            LocalFs
        }

        public FileSystemType FsType;
        public GameCardHandle GcHandle;
        public bool CanMountNca;

        public MountInfo()
        {
            FsType = FileSystemType.None;
            GcHandle = 0;
            CanMountNca = false;
        }

        public readonly bool IsGameCard() => FsType == FileSystemType.GameCard;
        public readonly bool IsHostOrLocalFs() => FsType == FileSystemType.HostFs || FsType == FileSystemType.LocalFs;
    }

    public FileSystemServer FsServer => _config.FsServer;

    public NcaFileSystemServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        _updatePartitionPath = new UpdatePartitionPath();
        _externalKeyManager = new ExternalKeyManager();
        _systemDataUpdateEventManager = new SystemDataUpdateEventManager();

        _romFsDeepRetryStartCount = 0;
        _romFsRemountForDataCorruptionCount = 0;
        _romfsUnrecoverableDataCorruptionByRemountCount = 0;
        _romFsRecoveredByInvalidateCacheCount = 0;
        _romFsUnrecoverableByGameCardAccessFailedCount = 0;

        _romfsCountMutex = new SdkMutexType();
    }

    public void Dispose()
    {
        _updatePartitionPath.Dispose();
    }

    public long GetAddOnContentDivisionSize()
    {
        throw new NotImplementedException();
    }

    public long GetRomDivisionSize()
    {
        throw new NotImplementedException();
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path,
        ContentAttributes attributes, FileSystemProxyType type, bool canMountSystemDataPrivate, ulong id,
        bool isDirectory)
    {
        throw new NotImplementedException();
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path,
        ContentAttributes attributes, FileSystemProxyType type, ulong id, bool isDirectory)
    {
        throw new NotImplementedException();
    }

    public Result OpenFileSystem(ref SharedRef<IFileSystem> outFileSystem, out CodeVerificationData outVerificationData,
        ref readonly Path path, ContentAttributes attributes, FileSystemProxyType type, bool canMountSystemDataPrivate,
        ulong id, bool isDirectory)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path,
        ContentAttributes attributes, FileSystemProxyType fsType, ulong programId, bool isDirectory)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataFileSystem(ref SharedRef<IFileSystem> outFileSystem)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataStorage(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out Hash outNcaHeaderDigest,
        ref readonly Path path, ContentAttributes attributes, FileSystemProxyType fsType, ulong id)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataStorage(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out Hash outNcaHeaderDigest,
        ref readonly Path path, ContentAttributes attributes, FileSystemProxyType fsType, ulong id,
        bool canMountSystemDataPrivate)
    {
        throw new NotImplementedException();
    }

    public Result OpenStorageWithPatch(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out Hash ncaHeaderDigest,
        ref readonly Path originalNcaPath, ContentAttributes originalAttributes, ref readonly Path currentNcaPath,
        ContentAttributes currentAttributes, FileSystemProxyType fsType, ulong originalId, ulong currentId)
    {
        throw new NotImplementedException();
    }

    public Result OpenStorageWithPatch(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, out Hash ncaHeaderDigest,
        ref readonly Path originalNcaPath, ContentAttributes originalAttributes, ref readonly Path currentNcaPath,
        ContentAttributes currentAttributes, FileSystemProxyType fsType, ulong originalId, ulong currentId,
        bool canMountSystemDataPrivate)
    {
        throw new NotImplementedException();
    }

    public Result OpenFileSystemWithPatch(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path originalNcaPath,
        ContentAttributes originalAttributes, ref readonly Path currentNcaPath, ContentAttributes currentAttributes,
        FileSystemProxyType fsType, ulong originalId, ulong currentId)
    {
        throw new NotImplementedException();
    }

    public Result OpenContentStorageFileSystem(ref SharedRef<IFileSystem> outFileSystem,
        ContentStorageId contentStorageId)
    {
        throw new NotImplementedException();
    }

    public Result GetRightsId(out RightsId rightsId, out byte keyGeneration, ref readonly Path path,
        ContentAttributes attributes, ProgramId programId)
    {
        throw new NotImplementedException();
    }

    public Result GetProgramId(out ProgramId outProgramId, ref readonly Path path, ContentAttributes attributes, ProgramId programId)
    {
        throw new NotImplementedException();
    }

    public Result RegisterExternalKey(in RightsId rightsId, in AccessKey accessKey)
    {
        return _externalKeyManager.Register(in rightsId, in accessKey).Ret();
    }

    public Result UnregisterExternalKey(in RightsId rightsId)
    {
        return _externalKeyManager.Unregister(in rightsId).Ret();
    }

    public Result UnregisterAllExternalKey()
    {
        return _externalKeyManager.UnregisterAll().Ret();
    }

    public Result RegisterUpdatePartition(ulong programId, ref readonly Path path, ContentAttributes attributes)
    {
        return _updatePartitionPath.Set(programId, in path, attributes).Ret();
    }

    public Result OpenRegisteredUpdatePartition(ref SharedRef<IFileSystem> outFileSystem)
    {
        using var path = new Path();
        Result res = _updatePartitionPath.Get(ref path.Ref(), out ContentAttributes contentAttributes, out ulong updaterProgramId);
        if (res.IsFailure()) return res.Miss();

        return OpenFileSystem(ref outFileSystem, in path, contentAttributes, FileSystemProxyType.RegisteredUpdate,
            updaterProgramId, isDirectory: false).Ret();
    }

    private Result ParseMountName(ref U8Span path, ref SharedRef<IFileSystem> outFileSystem, out MountInfo outMountInfo)
    {
        throw new NotImplementedException();
    }

    private Result CheckNcaOrNsp(ref U8Span path)
    {
        throw new NotImplementedException();
    }

    private Result ParseDir(ref readonly Path path, ref SharedRef<IFileSystem> outContentFileSystem,
        ref SharedRef<IFileSystem> baseFileSystem, FileSystemProxyType fsType, bool preserveUnc)
    {
        using var fileSystem = new SharedRef<IFileSystem>();
        Result res = _config.SubDirectoryFsCreator.Create(ref fileSystem.Ref, baseFileSystem, path);
        if (res.IsFailure()) return res.Miss();

        return ParseContentTypeForDirectory(ref outContentFileSystem, ref fileSystem.Ref, fsType);
    }

    private Result ParseDirWithPathCaseNormalizationOnCaseSensitiveHostOrLocalFs(
        ref SharedRef<IFileSystem> outFileSystem, ref readonly Path path, MountInfo.FileSystemType fsType)
    {
        throw new NotImplementedException();
    }

    private Result ParseNsp(out bool outFoundNspPath, ref U8Span path, ref SharedRef<IFileSystem> outFileSystem,
        ref readonly SharedRef<IFileSystem> baseFileSystem)
    {
        throw new NotImplementedException();
    }

    private Result ParseNca(ref SharedRef<NcaReader> outNcaReader, ref readonly SharedRef<IFileSystem> baseFileSystem,
        U8Span path, ContentAttributes attributes, ulong programId)
    {
        throw new NotImplementedException();
    }

    private Result ParseNca(ref SharedRef<NcaReader> outNcaReader, out bool outIsGameCard, U8Span path,
        ContentAttributes attributes, ulong programId)
    {
        throw new NotImplementedException();
    }

    private Result ParseContentTypeForDirectory(ref SharedRef<IFileSystem> outFileSystem,
        ref readonly SharedRef<IFileSystem> baseFileSystem, FileSystemProxyType fsType)
    {
        throw new NotImplementedException();
    }

    public Result SetExternalKeyForRightsId(NcaReader ncaReader)
    {
        throw new NotImplementedException();
    }

    public bool IsAvailableKeySource(ReadOnlySpan<byte> keySource)
    {
        throw new NotImplementedException();
    }

    public Result OpenStorageByContentType(ref SharedRef<IStorage> outNcaStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter,
        ref readonly SharedRef<NcaReader> ncaReader, out NcaFsHeader.FsType outFsType, FileSystemProxyType fsProxyType,
        bool isGameCard, bool canMountSystemDataPrivate)
    {
        throw new NotImplementedException();
    }

    public Result OpenStorageWithPatchByContentType(ref SharedRef<IStorage> outNcaStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter,
        ref readonly SharedRef<NcaReader> originalNcaReader, ref readonly SharedRef<NcaReader> currentNcaReader,
        out NcaFsHeader.FsType outFsType, FileSystemProxyType fsProxyType, bool canMountSystemDataPrivate)
    {
        throw new NotImplementedException();
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed encryptionSeed)
    {
        _encryptionSeed = encryptionSeed;

        return Result.Success;
    }

    public Result ResolveRomReferenceProgramId(out ProgramId outTargetProgramId, ProgramId programId,
        byte programIndex)
    {
        UnsafeHelpers.SkipParamInit(out outTargetProgramId);

        ProgramId targetProgramId = _config.ProgramRegistryService.GetProgramIdByIndex(programId, programIndex);
        if (targetProgramId == ProgramId.InvalidId)
            return ResultFs.ProgramIndexNotFound.Log();

        outTargetProgramId = targetProgramId;
        return Result.Success;
    }

    public Result ResolveProgramPath(out bool isDirectory, ref Path outPath, out ContentAttributes outContentAttributes,
        ProgramId programId, StorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result ResolveApplicationControlPath(ref Path outPath, out ContentAttributes outContentAttributes,
        ApplicationId applicationId, StorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result ResolveRomPath(out bool isDirectory, ref Path outPath, out ContentAttributes outContentAttributes,
        out ulong outOriginalProgramId, ulong programId, StorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result ResolveApplicationHtmlDocumentPath(out bool isDirectory, ref Path outPath,
        out ContentAttributes outContentAttributes, out ulong outOriginalProgramId, ulong programId,
        StorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result ResolveDataPath(ref Path outPath, out ContentAttributes outContentAttributes, DataId dataId,
        StorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result ResolveAddOnContentPath(ref Path outPath, out ContentAttributes outContentAttributes,
        ref Path outPatchPath, out ContentAttributes outPatchContentAttributes, DataId dataId)
    {
        throw new NotImplementedException();
    }

    public Result ResolveRegisteredProgramPath(ref Path outPath, out ContentAttributes outContentAttributes,
        ulong programId)
    {
        throw new NotImplementedException();
    }

    public Result ResolveRegisteredHtmlDocumentPath(ref Path outPath, out ContentAttributes outContentAttributes,
        ulong programId)
    {
        throw new NotImplementedException();
    }

    internal StorageLayoutType GetStorageFlag(ulong programId)
    {
        Assert.SdkRequiresNotEqual(_config.SpeedEmulationRange.ProgramIdWithoutPlatformIdMax, 0ul);

        ulong programIdWithoutPlatformId = Utility.ClearPlatformIdInProgramId(programId);

        if (programIdWithoutPlatformId >= _config.SpeedEmulationRange.ProgramIdWithoutPlatformIdMin &&
            programIdWithoutPlatformId <= _config.SpeedEmulationRange.ProgramIdWithoutPlatformIdMax)
            return StorageLayoutType.Bis;
        else
            return StorageLayoutType.All;
    }

    public Result HandleResolubleAccessFailure(out bool wasDeferred, Result nonDeferredResult,
        ulong processId)
    {
        return _config.AccessFailureManagementService
            .HandleResolubleAccessFailure(out wasDeferred, nonDeferredResult, processId).Ret();
    }

    public void IncrementRomFsDeepRetryStartCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsDeepRetryStartCount++;
    }

    public void IncrementRomFsRemountForDataCorruptionCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsRemountForDataCorruptionCount++;
    }

    public void IncrementRomFsUnrecoverableDataCorruptionByRemountCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romfsUnrecoverableDataCorruptionByRemountCount++;
    }

    public void IncrementRomFsRecoveredByInvalidateCacheCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsRecoveredByInvalidateCacheCount++;
    }

    public void IncrementRomFsUnrecoverableByGameCardAccessFailedCount()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);
        _romFsUnrecoverableByGameCardAccessFailedCount++;
    }

    public void GetAndClearRomFsErrorInfo(out uint outDeepRetryStartCount, out uint outRemountForDataCorruptionCount,
        out uint outUnrecoverableDataCorruptionByRemountCount, out uint outRecoveredByInvalidateCacheCount,
        out uint outUnrecoverableByGameCardAccessFailedCount)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _romfsCountMutex);

        outDeepRetryStartCount = _romFsDeepRetryStartCount;
        outRemountForDataCorruptionCount = _romFsRemountForDataCorruptionCount;
        outUnrecoverableDataCorruptionByRemountCount = _romfsUnrecoverableDataCorruptionByRemountCount;
        outRecoveredByInvalidateCacheCount = _romFsRecoveredByInvalidateCacheCount;
        outUnrecoverableByGameCardAccessFailedCount = _romFsUnrecoverableByGameCardAccessFailedCount;

        _romFsDeepRetryStartCount = 0;
        _romFsRemountForDataCorruptionCount = 0;
        _romfsUnrecoverableDataCorruptionByRemountCount = 0;
        _romFsRecoveredByInvalidateCacheCount = 0;
        _romFsUnrecoverableByGameCardAccessFailedCount = 0;
    }

    public Result CreateNotifier(ref UniqueRef<SystemDataUpdateEventNotifier> outNotifier)
    {
        throw new NotImplementedException();
    }

    public Result NotifySystemDataUpdateEvent()
    {
        throw new NotImplementedException();
    }

    public Result OpenHostFileSystem(ref SharedRef<IFileSystem> outFileSystem, ref readonly Path rootPath, bool openCaseSensitive)
    {
        return _config.TargetManagerFsCreator.Create(ref outFileSystem, in rootPath, openCaseSensitive, false,
            Result.Success).Ret();
    }

    internal Result GetProgramInfoByProcessId(out ProgramInfo programInfo, ulong processId)
    {
        var registry = new ProgramRegistryImpl(_config.FsServer);
        return registry.GetProgramInfo(out programInfo, processId);
    }

    internal Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
    {
        var registry = new ProgramRegistryImpl(_config.FsServer);
        return registry.GetProgramInfoByProgramId(out programInfo, programId);
    }
}

public readonly struct InternalProgramIdRangeForSpeedEmulation
{
    public readonly ulong ProgramIdWithoutPlatformIdMin;
    public readonly ulong ProgramIdWithoutPlatformIdMax;

    public InternalProgramIdRangeForSpeedEmulation(ulong min, ulong max)
    {
        ProgramIdWithoutPlatformIdMin = min;
        ProgramIdWithoutPlatformIdMax = max;
    }
}