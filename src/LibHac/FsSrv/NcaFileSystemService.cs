using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Lr;
using LibHac.Ncm;
using LibHac.Sf;
using LibHac.Spl;
using LibHac.Util;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorage = LibHac.Fs.IStorage;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;
using Path = LibHac.Fs.Path;

namespace LibHac.FsSrv;

/// <summary>
/// Handles NCA-related calls for <see cref="FileSystemProxyImpl"/>.
/// </summary>
/// <remarks>FS will have one instance of this class for every connected process.
/// The FS permissions of the calling process are checked on every function call.
/// <br/>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
internal class NcaFileSystemService : IRomFileSystemAccessFailureManager
{
    private const int AocSemaphoreCount = 128;
    private const int RomSemaphoreCount = 10;
    private const int RomDivisionSizeUnitCountSemaphoreCount = 128;

    private WeakRef<NcaFileSystemService> _selfReference;
    private readonly NcaFileSystemServiceImpl _serviceImpl;
    private ulong _processId;
    private SemaphoreAdapter _aocMountCountSemaphore;
    private SemaphoreAdapter _romMountCountSemaphore;
    private SemaphoreAdapter _romDivisionSizeUnitCountSemaphore;

    private NcaFileSystemService(NcaFileSystemServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
        _aocMountCountSemaphore = new SemaphoreAdapter(AocSemaphoreCount, AocSemaphoreCount);
        _romMountCountSemaphore = new SemaphoreAdapter(RomSemaphoreCount, RomSemaphoreCount);
        _romDivisionSizeUnitCountSemaphore = new SemaphoreAdapter(RomDivisionSizeUnitCountSemaphoreCount, RomDivisionSizeUnitCountSemaphoreCount);
    }

    private SharedRef<NcaFileSystemService> GetSharedFromThis() =>
        SharedRef<NcaFileSystemService>.Create(in _selfReference);

    private SharedRef<IRomFileSystemAccessFailureManager> GetSharedAccessFailureManagerFromThis() =>
        SharedRef<IRomFileSystemAccessFailureManager>.Create(in _selfReference);

    public static SharedRef<NcaFileSystemService> CreateShared(NcaFileSystemServiceImpl serviceImpl,
        ulong processId)
    {
        // Create the service
        var ncaService = new NcaFileSystemService(serviceImpl, processId);

        // Wrap the service in a ref-counter and give the service a weak self-reference
        using var sharedService = new SharedRef<NcaFileSystemService>(ncaService);
        ncaService._selfReference.Set(in sharedService);

        return SharedRef<NcaFileSystemService>.CreateMove(ref sharedService.Ref);
    }

    public void Dispose()
    {
        _aocMountCountSemaphore?.Dispose();
        _romMountCountSemaphore?.Dispose();
        _romDivisionSizeUnitCountSemaphore?.Dispose();
        _selfReference.Destroy();
    }

    private Result GetProgramInfo(out ProgramInfo programInfo)
    {
        var programRegistry = new ProgramRegistryImpl(_serviceImpl.FsServer);
        return programRegistry.GetProgramInfo(out programInfo, _processId).Ret();
    }

    private Result GetProgramInfoByProcessId(out ProgramInfo programInfo, ulong processId)
    {
        var programRegistry = new ProgramRegistryImpl(_serviceImpl.FsServer);
        return programRegistry.GetProgramInfo(out programInfo, processId).Ret();
    }

    private Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
    {
        var programRegistry = new ProgramRegistryImpl(_serviceImpl.FsServer);
        return programRegistry.GetProgramInfoByProgramId(out programInfo, programId).Ret();
    }

    public Result OpenFileSystemWithPatch(ref SharedRef<IFileSystemSf> outFileSystem, ProgramId programId,
        FileSystemProxyType type)
    {
        const StorageLayoutType storageFlag = StorageLayoutType.All;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        // Get the program info for the caller and verify permissions
        Result res = GetProgramInfo(out ProgramInfo callerProgramInfo);
        if (res.IsFailure()) return res.Miss();

        switch (type)
        {
            case FileSystemProxyType.Manual:
                Accessibility accessibility =
                    callerProgramInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountContentManual);

                if (!accessibility.CanRead)
                    return ResultFs.PermissionDenied.Log();

                break;
            case FileSystemProxyType.Logo:
            case FileSystemProxyType.Control:
            case FileSystemProxyType.Meta:
            case FileSystemProxyType.Data:
            case FileSystemProxyType.Package:
                return ResultFs.NotImplemented.Log();
            default:
                return ResultFs.InvalidArgument.Log();
        }

        // Get the program info for the owner of the file system being opened
        res = GetProgramInfoByProgramId(out ProgramInfo ownerProgramInfo, programId.Value);
        if (res.IsFailure()) return res.Miss();

        // Try to find the path to the original version of the file system
        using var originalPath = new Path();
        Result originalResult = _serviceImpl.ResolveApplicationHtmlDocumentPath(out bool isDirectory,
            ref originalPath.Ref(), out ContentAttributes contentAttributes, out ulong originalProgramId,
            programId.Value, ownerProgramInfo.StorageId);

        // The file system might have a patch version with no original version, so continue if not found
        if (originalResult.IsFailure() && !ResultLr.HtmlDocumentNotFound.Includes(originalResult))
            return originalResult.Miss();

        // Try to find the path to the patch file system
        using var patchPath = new Path();
        Result patchResult = _serviceImpl.ResolveRegisteredHtmlDocumentPath(ref patchPath.Ref(),
            out ContentAttributes patchContentAttributes, programId.Value);

        using var fileSystem = new SharedRef<IFileSystem>();

        if (ResultLr.HtmlDocumentNotFound.Includes(patchResult))
        {
            // There must either be an original version or patch version of the file system being opened
            if (originalResult.IsFailure())
                return originalResult.Miss();

            // There is an original version and no patch version. Open the original directly
            res = _serviceImpl.OpenFileSystem(ref fileSystem.Ref, in originalPath, contentAttributes, type,
                programId.Value, isDirectory);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            if (patchResult.IsFailure())
                return patchResult.Miss();

            ref readonly Path originalNcaPath = ref originalResult.IsSuccess()
                ? ref originalPath
                : ref PathExtensions.GetNullRef();

            // Open the file system using both the original and patch versions
            res = _serviceImpl.OpenFileSystemWithPatch(ref fileSystem.Ref, in originalNcaPath, contentAttributes,
                in patchPath, patchContentAttributes, type, originalProgramId, programId.Value);
            if (res.IsFailure()) return res.Miss();
        }

        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));

        using SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager = SharedRef<IRomFileSystemAccessFailureManager>.Create(in _selfReference);
        using SharedRef<IFileSystem> retryFileSystem = DeepRetryFileSystem.CreateShared(in asyncFileSystem, in accessFailureManager);
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in retryFileSystem, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenCodeFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, OutBuffer outVerificationData,
        ref readonly FspPath path, ContentAttributes attributes, ProgramId programId)
    {
        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(programId.Value);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        if (!_serviceImpl.FsServer.IsInitialProgram(_processId))
            return ResultFs.PermissionDenied.Log();

        bool isDirectory = PathUtility.IsDirectoryPath(in path);
        using var pathNormalized = new Path();
        Result res = pathNormalized.InitializeWithReplaceUnc(path.Str);
        if (res.IsFailure()) return res.Miss();

        var flags = new PathFlags();
        flags.AllowMountName();
        flags.AllowWindowsPath();
        res = pathNormalized.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        if (outVerificationData.Size != Unsafe.SizeOf<CodeVerificationData>())
            return ResultFs.InvalidArgument.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenFileSystem(ref fileSystem.Ref, ref outVerificationData.As<CodeVerificationData>(),
            in pathNormalized, attributes, FileSystemProxyType.Code, canMountSystemDataPrivate: false, programId.Value,
            isDirectory);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenDataFileSystemByCurrentProcess(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = OpenDataFileSystemCore(ref fileSystem.Ref, out bool isHostFs, programInfo.ProgramId.Value, programInfo.StorageId);
        if (res.IsFailure()) return res.Miss();

        using var asyncFileSystem = new SharedRef<IFileSystem>();

        if (isHostFs)
        {
            asyncFileSystem.SetByMove(ref fileSystem.Ref);
        }
        else
        {
            asyncFileSystem.Reset(new AsynchronousAccessFileSystem(in fileSystem));
        }

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    private Result TryAcquireAddOnContentDivisionSizeUnitCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphore, IStorage storage)
    {
        Result res = storage.GetSize(out long storageSize);
        if (res.IsFailure()) return res.Miss();

        int divisionCount = (int)BitUtil.DivideUp(storageSize, _serviceImpl.GetAddOnContentDivisionSize());

        using SharedRef<NcaFileSystemService> ncaService = GetSharedFromThis();
        res = FsSystem.Utility.MakeUniqueLockWithPin(ref outSemaphore, _aocMountCountSemaphore, divisionCount, in ncaService);

        if (!res.IsSuccess())
        {
            if (ResultFs.OpenCountLimit.Includes(res))
                return ResultFs.AocMountDivisionSizeUnitCountLimit.LogConverted(res);

            return res.Miss();
        }

        return Result.Success;
    }

    private Result TryAcquireRomMountCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphore)
    {
        using SharedRef<NcaFileSystemService> ncaService = GetSharedFromThis();
        Result res = FsSystem.Utility.MakeUniqueLockWithPin(ref outSemaphore, _romMountCountSemaphore, ref ncaService.Ref);

        if (!res.IsSuccess())
        {
            if (ResultFs.OpenCountLimit.Includes(res))
                return ResultFs.RomMountCountLimit.LogConverted(res);

            return res.Miss();
        }

        return Result.Success;
    }

    private Result TryAcquireRomDivisionSizeUnitCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphore,
        ref UniqueRef<IUniqueLock> mountCountSemaphore, IStorage storage)
    {
        Result res = storage.GetSize(out long storageSize);
        if (res.IsFailure()) return res.Miss();

        int divisionCount = (int)BitUtil.DivideUp(storageSize, _serviceImpl.GetRomDivisionSize());

        using SharedRef<NcaFileSystemService> ncaService = GetSharedFromThis();
        res = FsSystem.Utility.MakeUniqueLockWithPin(ref outSemaphore, ref mountCountSemaphore,
            _romDivisionSizeUnitCountSemaphore, divisionCount, in ncaService);

        if (!res.IsSuccess())
        {
            if (ResultFs.OpenCountLimit.Includes(res))
                return ResultFs.RomMountDivisionSizeUnitCountLimit.LogConverted(res);

            return res.Miss();
        }

        return Result.Success;
    }

    public void IncrementRomFsDeepRetryStartCount()
    {
        _serviceImpl.IncrementRomFsDeepRetryStartCount();
    }

    public void IncrementRomFsRemountForDataCorruptionCount()
    {
        _serviceImpl.IncrementRomFsRemountForDataCorruptionCount();
    }

    public void IncrementRomFsUnrecoverableDataCorruptionByRemountCount()
    {
        _serviceImpl.IncrementRomFsUnrecoverableDataCorruptionByRemountCount();
    }

    public void IncrementRomFsRecoveredByInvalidateCacheCount()
    {
        _serviceImpl.IncrementRomFsRecoveredByInvalidateCacheCount();
    }

    public void IncrementRomFsUnrecoverableByGameCardAccessFailedCount()
    {
        _serviceImpl.IncrementRomFsUnrecoverableByGameCardAccessFailedCount();
    }

    private Result OpenDataStorageCore(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, ref Hash outNcaDigest, ulong id,
        StorageId storageId)
    {
        using var programPath = new Path();
        Result originalResult = _serviceImpl.ResolveRomPath(out bool isDirectory, ref programPath.Ref(),
            out ContentAttributes contentAttributes, out ulong originalProgramId, id, storageId);

        using var patchPath = new Path();
        Result patchResult = _serviceImpl.ResolveRegisteredProgramPath(ref patchPath.Ref(),
            out ContentAttributes patchContentAttributes, id);

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();

        if (ResultLr.ProgramNotFound.Includes(patchResult))
        {
            // If a patch NCA wasn't found, operate on just the original NCA.
            // We can't open a storage if the content is from a directory
            if (isDirectory)
                return ResultFs.TargetNotFound.Log();

            // Since we couldn't find a patch NCA, make sure we successfully found an original NCA
            if (originalResult.IsFailure())
                return originalResult.Miss();

            Result res = _serviceImpl.OpenDataStorage(ref storage.Ref, ref storageAccessSplitter.Ref,
                ref outNcaDigest, in programPath, contentAttributes, FileSystemProxyType.Rom, id);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            if (patchResult.IsFailure())
                return patchResult.Miss();

            ref readonly Path originalNcaPath = ref originalResult.IsSuccess()
                ? ref programPath
                : ref PathExtensions.GetNullRef();

            Result res = _serviceImpl.OpenStorageWithPatch(ref storage.Ref, ref storageAccessSplitter.Ref,
                ref outNcaDigest, in originalNcaPath, contentAttributes, in patchPath, patchContentAttributes,
                FileSystemProxyType.Rom, originalProgramId, id);
            if (res.IsFailure()) return res.Miss();
        }

        outStorage.SetByMove(ref storage.Ref);
        outStorageAccessSplitter.SetByMove(ref storageAccessSplitter.Ref);

        return Result.Success;
    }

    public Result OpenDataStorageByCurrentProcess(ref SharedRef<IStorageSf> outStorage)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(programInfo.ProgramIdValue);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Hash digest = default;

        using var romMountCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomMountCountSemaphore(ref romMountCountSemaphore.Ref);
        if (res.IsFailure()) return res.Miss();

        StorageId storageId = programInfo.StorageId;
        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        res = OpenDataStorageCore(ref storage.Ref, ref storageAccessSplitter.Ref, ref digest, programInfo.ProgramIdValue, storageId);
        if (res.IsFailure()) return res.Miss();

        using var romDivisionSizeUnitCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomDivisionSizeUnitCountSemaphore(ref romDivisionSizeUnitCountSemaphore.Ref,
            ref romMountCountSemaphore.Ref, storage.Get);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager = GetSharedAccessFailureManagerFromThis();

        using var retryStorage = new SharedRef<IStorage>(new DeepRetryStorage(in storage, in storageAccessSplitter,
            in accessFailureManager, ref romDivisionSizeUnitCountSemaphore.Ref, in digest, programInfo.ProgramIdValue,
            storageId, _serviceImpl.FsServer));

        using var typeSetStorage = new SharedRef<IStorage>(new StorageLayoutTypeSetStorage(in retryStorage, storageFlag));
        using var storageAdapter = new SharedRef<IStorageSf>(new StorageInterfaceAdapter(in typeSetStorage));

        outStorage.SetByMove(ref storageAdapter.Ref);

        return Result.Success;
    }

    public Result OpenDataStorageByPath(ref SharedRef<IStorageSf> outStorage, in FspPath path,
        ContentAttributes attributes, FileSystemProxyType type)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(programInfo.ProgramIdValue);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        if (!programInfo.AccessControl.CanCall(OperationType.OpenDataStorageByPath))
            return ResultFs.PermissionDenied.Log();

        using var storage = new SharedRef<IStorage>();
        Hash digest = default;

        using var romMountCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomMountCountSemaphore(ref romMountCountSemaphore.Ref);
        if (res.IsFailure()) return res.Miss();

        using var ncaPath = new Path();
        res = ncaPath.Initialize(path.Str);
        if (res.IsFailure()) return res.Miss();

        var flags = new PathFlags();
        flags.AllowMountName();
        res = ncaPath.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        res = _serviceImpl.OpenDataStorage(ref storage.Ref, ref storageAccessSplitter.Ref, ref digest, in ncaPath,
            attributes, type, ulong.MaxValue);
        if (res.IsFailure()) return res.Miss();

        using var romDivisionSizeUnitCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomDivisionSizeUnitCountSemaphore(ref romDivisionSizeUnitCountSemaphore.Ref,
            ref romMountCountSemaphore.Ref, storage.Get);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager = GetSharedAccessFailureManagerFromThis();

        using var retryStorage = new SharedRef<IStorage>(new DeepRetryStorage(in storage, in storageAccessSplitter,
            in accessFailureManager, ref romDivisionSizeUnitCountSemaphore.Ref, in digest, ProgramId.InvalidId.Value,
            StorageId.None, _serviceImpl.FsServer));

        using var typeSetStorage = new SharedRef<IStorage>(new StorageLayoutTypeSetStorage(in retryStorage, storageFlag));
        using var storageAdapter = new SharedRef<IStorageSf>(new StorageInterfaceAdapter(in typeSetStorage));

        outStorage.SetByMove(ref storageAdapter.Ref);

        return Result.Success;
    }

    public Result OpenDataStorageByProgramId(ref SharedRef<IStorageSf> outStorage, ProgramId programId)
    {
        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(programId.Value);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfoByProgramId(out ProgramInfo programInfo, programId.Value);
        if (res.IsFailure()) return res.Miss();

        Hash digest = default;

        using var romMountCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomMountCountSemaphore(ref romMountCountSemaphore.Ref);
        if (res.IsFailure()) return res.Miss();

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        res = OpenDataStorageCore(ref storage.Ref, ref storageAccessSplitter.Ref, ref digest, programId.Value, programInfo.StorageId);
        if (res.IsFailure()) return res.Miss();

        res = GetProgramInfo(out ProgramInfo properProgramInfo);
        if (res.IsFailure()) return res.Miss();

        if (!properProgramInfo.AccessControl.HasContentOwnerId(programId.Value))
            return ResultFs.PermissionDenied.Log();

        using var romDivisionSizeUnitCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomDivisionSizeUnitCountSemaphore(ref romDivisionSizeUnitCountSemaphore.Ref,
            ref romMountCountSemaphore.Ref, storage.Get);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager = GetSharedAccessFailureManagerFromThis();

        using var retryStorage = new SharedRef<IStorage>(new DeepRetryStorage(in storage, in storageAccessSplitter,
            in accessFailureManager, ref romDivisionSizeUnitCountSemaphore.Ref, in digest, programId.Value,
            programInfo.StorageId, _serviceImpl.FsServer));

        using var typeSetStorage = new SharedRef<IStorage>(new StorageLayoutTypeSetStorage(in retryStorage, storageFlag));
        using var storageAdapter = new SharedRef<IStorageSf>(new StorageInterfaceAdapter(in typeSetStorage));

        outStorage.SetByMove(ref storageAdapter.Ref);

        return Result.Success;
    }

    public Result OpenFileSystemWithId(ref SharedRef<IFileSystemSf> outFileSystem, in FspPath path,
        ContentAttributes attributes, ulong id, FileSystemProxyType type)
    {
        const StorageLayoutType storageFlag = StorageLayoutType.All;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        AccessControl ac = programInfo.AccessControl;

        switch (type)
        {
            case FileSystemProxyType.Logo:
                if (!ac.GetAccessibilityFor(AccessibilityType.MountLogo).CanRead)
                    return ResultFs.PermissionDenied.Log();
                break;
            case FileSystemProxyType.Control:
                if (!ac.GetAccessibilityFor(AccessibilityType.MountContentControl).CanRead)
                    return ResultFs.PermissionDenied.Log();
                break;
            case FileSystemProxyType.Manual:
                if (!ac.GetAccessibilityFor(AccessibilityType.MountContentManual).CanRead)
                    return ResultFs.PermissionDenied.Log();
                break;
            case FileSystemProxyType.Meta:
                if (!ac.GetAccessibilityFor(AccessibilityType.MountContentMeta).CanRead)
                    return ResultFs.PermissionDenied.Log();
                break;
            case FileSystemProxyType.Data:
                if (!ac.GetAccessibilityFor(AccessibilityType.MountContentData).CanRead)
                    return ResultFs.PermissionDenied.Log();
                break;
            case FileSystemProxyType.Package:
                if (!ac.GetAccessibilityFor(AccessibilityType.MountApplicationPackage).CanRead)
                    return ResultFs.PermissionDenied.Log();
                break;
            default:
                return ResultFs.InvalidArgument.Log();
        }

        if (type == FileSystemProxyType.Meta)
        {
            id = ulong.MaxValue;
        }
        else if (id == ulong.MaxValue)
        {
            return ResultFs.InvalidArgument.Log();
        }

        bool canMountSystemDataPrivate = ac.GetAccessibilityFor(AccessibilityType.MountSystemDataPrivate).CanRead;

        using var pathNormalized = new Path();
        res = pathNormalized.InitializeWithReplaceUnc(path.Str);
        if (res.IsFailure()) return res.Miss();

        var flags = new PathFlags();
        flags.AllowMountName();
        flags.AllowWindowsPath();
        res = pathNormalized.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        bool isDirectory = PathUtility.IsDirectoryPath(in path);

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenFileSystem(ref fileSystem.Ref, in pathNormalized, attributes, type,
            canMountSystemDataPrivate, id, isDirectory);
        if (res.IsFailure()) return res.Miss();

        // Add all the wrappers for the file system
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenDataFileSystemByProgramId(ref SharedRef<IFileSystemSf> outFileSystem, ProgramId programId)
    {
        Result res = GetProgramInfoByProgramId(out ProgramInfo programInfo, programId.Value);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = OpenDataFileSystemCore(ref fileSystem.Ref, out _, programInfo.ProgramId.Value, programInfo.StorageId);
        if (res.IsFailure()) return res.Miss();

        res = GetProgramInfo(out ProgramInfo properProgramInfo);
        if (res.IsFailure()) return res.Miss();

        if (!properProgramInfo.AccessControl.HasContentOwnerId(programId.Value))
            return ResultFs.PermissionDenied.Log();

        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in fileSystem));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenDataStorageByDataId(ref SharedRef<IStorageSf> outStorage, DataId dataId, StorageId storageId)
    {
        Result res;

        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(dataId.Value);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        bool isAoc = storageId == StorageId.None;

        using var systemDataPath = new Path();
        using var systemDataPatchPath = new Path();

        ContentAttributes contentAttributes;
        ContentAttributes patchContentAttributes = ContentAttributes.None;

        if (isAoc)
        {
            res = _serviceImpl.ResolveAddOnContentPath(ref systemDataPath.Ref(), out contentAttributes,
                ref systemDataPatchPath.Ref(), out patchContentAttributes, dataId);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = _serviceImpl.ResolveDataPath(ref systemDataPath.Ref(), out contentAttributes, dataId, storageId);
            if (res.IsFailure()) return res.Miss();

            Assert.SdkAssert(systemDataPatchPath.IsEmpty());
        }

        res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        var accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountSystemDataPrivate);
        bool canMountSystemDataPrivate = accessibility.CanRead;

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();

        if (systemDataPatchPath.IsEmpty())
        {
            res = _serviceImpl.OpenDataStorage(ref storage.Ref, ref storageAccessSplitter.Ref,
                ref Unsafe.NullRef<Hash>(), in systemDataPath, contentAttributes, FileSystemProxyType.Data,
                dataId.Value, canMountSystemDataPrivate);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = _serviceImpl.OpenStorageWithPatch(ref storage.Ref, ref storageAccessSplitter.Ref,
                ref Unsafe.NullRef<Hash>(), in systemDataPath, contentAttributes, in systemDataPatchPath,
                patchContentAttributes, FileSystemProxyType.Data, dataId.Value, dataId.Value, canMountSystemDataPrivate);
            if (res.IsFailure()) return res.Miss();
        }

        using var mountCountSemaphore = new UniqueRef<IUniqueLock>();

        if (isAoc)
        {
            res = TryAcquireAddOnContentDivisionSizeUnitCountSemaphore(ref mountCountSemaphore.Ref, storage.Get);
            if (res.IsFailure()) return res.Miss();
        }

        using SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager = GetSharedAccessFailureManagerFromThis();

        using var retryStorage = new SharedRef<IStorage>(new DeepRetryStorage(in storage, in storageAccessSplitter,
            in accessFailureManager, ref mountCountSemaphore.Ref, deepRetryEnabled: isAoc, _serviceImpl.FsServer));

        using var typeSetStorage = new SharedRef<IStorage>(new StorageLayoutTypeSetStorage(in retryStorage, storageFlag));
        using var storageAdapter = new SharedRef<IStorageSf>(new StorageInterfaceAdapter(in typeSetStorage));

        outStorage.SetByMove(ref storageAdapter.Ref);

        return Result.Success;
    }

    public Result OpenDataFileSystemByDataId(ref SharedRef<IFileSystemSf> outFileSystem, DataId dataId, StorageId storageId)
    {
        Result res;

        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(dataId.Value);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        bool isAoc = storageId == StorageId.None;

        using var dataPath = new Path();
        using var dataPatchPathUnused = new Path();

        ContentAttributes contentAttributes;

        if (isAoc)
        {
            res = _serviceImpl.ResolveAddOnContentPath(ref dataPath.Ref(), out contentAttributes,
                ref dataPatchPathUnused.Ref(), out _, dataId);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            res = _serviceImpl.ResolveDataPath(ref dataPath.Ref(), out contentAttributes, dataId, storageId);
            if (res.IsFailure()) return res.Miss();

            Assert.SdkAssert(dataPatchPathUnused.IsEmpty());
        }

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenDataFileSystem(ref fileSystem.Ref, in dataPath, contentAttributes,
            FileSystemProxyType.Data, dataId.Value, isDirectory: true);

        if (!res.IsSuccess())
        {
            if (ResultFs.PathNotFound.Includes(res))
                return ResultFs.TargetNotFound.LogConverted(res);

            return res.Miss();
        }

        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in typeSetFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenPatchDataStorageByCurrentProcess(ref SharedRef<IStorageSf> outStorage)
    {
        return ResultFs.TargetNotFound.Log();
    }

    public Result OpenDataFileSystemWithProgramIndex(ref SharedRef<IFileSystemSf> outFileSystem, byte programIndex)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        // Get the program ID of the program with the specified index if in a multi-program application
        res = _serviceImpl.ResolveRomReferenceProgramId(out ProgramId targetProgramId, programInfo.ProgramId,
            programIndex);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = OpenDataFileSystemCore(ref fileSystem.Ref, out _, targetProgramId.Value, programInfo.StorageId);
        if (res.IsFailure()) return res.Miss();

        // Verify the caller has access to the file system
        if (targetProgramId != programInfo.ProgramId &&
            !programInfo.AccessControl.HasContentOwnerId(targetProgramId.Value))
        {
            return ResultFs.PermissionDenied.Log();
        }

        // Add all the file system wrappers
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in fileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenDataStorageWithProgramIndex(ref SharedRef<IStorageSf> outStorage, byte programIndex)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        // Get the program ID of the program with the specified index if in a multi-program application
        res = _serviceImpl.ResolveRomReferenceProgramId(out ProgramId targetProgramId, programInfo.ProgramId, programIndex);
        if (res.IsFailure()) return res.Miss();

        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(targetProgramId.Value);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Hash digest = default;

        using var romMountCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomMountCountSemaphore(ref romMountCountSemaphore.Ref);
        if (res.IsFailure()) return res.Miss();

        using var storage = new SharedRef<IStorage>();
        using var storageAccessSplitter = new SharedRef<IAsynchronousAccessSplitter>();
        res = OpenDataStorageCore(ref storage.Ref, ref storageAccessSplitter.Ref, ref digest, targetProgramId.Value, programInfo.StorageId);
        if (res.IsFailure()) return res.Miss();

        if (programInfo.ProgramId != targetProgramId && !programInfo.AccessControl.HasContentOwnerId(targetProgramId.Value))
            return ResultFs.PermissionDenied.Log();

        using var romDivisionSizeUnitCountSemaphore = new UniqueRef<IUniqueLock>();
        res = TryAcquireRomDivisionSizeUnitCountSemaphore(ref romDivisionSizeUnitCountSemaphore.Ref,
            ref romMountCountSemaphore.Ref, storage.Get);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager = GetSharedAccessFailureManagerFromThis();

        using var retryStorage = new SharedRef<IStorage>(new DeepRetryStorage(in storage, in storageAccessSplitter,
            in accessFailureManager, ref romDivisionSizeUnitCountSemaphore.Ref, in digest, targetProgramId.Value,
            programInfo.StorageId, _serviceImpl.FsServer));

        using var typeSetStorage = new SharedRef<IStorage>(new StorageLayoutTypeSetStorage(in retryStorage, storageFlag));
        using var storageAdapter = new SharedRef<IStorageSf>(new StorageInterfaceAdapter(in typeSetStorage));

        outStorage.SetByMove(ref storageAdapter.Ref);

        return Result.Success;
    }

    public Result GetRightsId(out RightsId outRightsId, ProgramId programId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outRightsId);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.All);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.GetRightsId))
            return ResultFs.PermissionDenied.Log();

        using var programPath = new Path();
        res = _serviceImpl.ResolveProgramPath(out bool isDirectory, ref programPath.Ref(),
            out ContentAttributes contentAttributes, programId, storageId);
        if (res.IsFailure()) return res.Miss();

        if (isDirectory)
            return ResultFs.TargetNotFound.Log();

        res = _serviceImpl.GetRightsId(out RightsId rightsId, out _, in programPath, contentAttributes, programId);
        if (res.IsFailure()) return res.Miss();

        outRightsId = rightsId;

        return Result.Success;
    }

    public Result GetRightsIdAndKeyGenerationByPath(out RightsId outRightsId, out byte outKeyGeneration,
        ref readonly FspPath path, ContentAttributes attributes)
    {
        UnsafeHelpers.SkipParamInit(out outRightsId, out outKeyGeneration);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.All);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.GetRightsId))
            return ResultFs.PermissionDenied.Log();

        using var pathNormalized = new Path();
        res = pathNormalized.Initialize(path.Str);
        if (res.IsFailure()) return res.Miss();

        var flags = new PathFlags();
        flags.AllowMountName();
        flags.AllowWindowsPath();
        res = pathNormalized.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        if (PathUtility.IsDirectoryPath(in path))
            return ResultFs.TargetNotFound.Log();

        const ulong checkThroughProgramId = ulong.MaxValue;

        res = _serviceImpl.GetRightsId(out RightsId rightsId, out byte keyGeneration, in pathNormalized, attributes,
            new ProgramId(checkThroughProgramId));
        if (res.IsFailure()) return res.Miss();

        outRightsId = rightsId;
        outKeyGeneration = keyGeneration;

        return Result.Success;
    }

    public Result GetProgramId(out ProgramId outProgramId, ref readonly FspPath path, ContentAttributes attributes)
    {
        UnsafeHelpers.SkipParamInit(out outProgramId);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.All);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.GetProgramId))
            return ResultFs.PermissionDenied.Log();

        using var pathNormalized = new Path();
        res = pathNormalized.Initialize(path.Str);
        if (res.IsFailure()) return res.Miss();

        var flags = new PathFlags();
        flags.AllowMountName();
        flags.AllowWindowsPath();
        res = pathNormalized.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        if (PathUtility.IsDirectoryPath(in path))
            return ResultFs.TargetNotFound.Log();

        res = _serviceImpl.GetProgramId(out ProgramId programId, in pathNormalized, attributes);
        if (res.IsFailure()) return res.Miss();

        outProgramId = programId;
        return Result.Success;
    }

    private Result OpenDataFileSystemCore(ref SharedRef<IFileSystem> outFileSystem, out bool isHostFs, ulong programId,
        StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out isHostFs);

        StorageLayoutType storageFlag = _serviceImpl.GetStorageFlag(programId);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        using var programPath = new Path();
        Result res = _serviceImpl.ResolveRomPath(out bool isDirectory, ref programPath.Ref(),
            out ContentAttributes contentAttributes, out _, programId, storageId);
        if (res.IsFailure()) return res.Miss();

        isHostFs = Impl.Utility.IsHostFsMountName(programPath.GetString());

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenDataFileSystem(ref fileSystem.Ref, in programPath, contentAttributes,
            FileSystemProxyType.Rom, programId, isDirectory);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));

        outFileSystem.SetByMove(ref typeSetFileSystem.Ref);

        return Result.Success;
    }

    public Result OpenContentStorageFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        ContentStorageId contentStorageId)
    {
        StorageLayoutType storageFlag = contentStorageId == ContentStorageId.System ? StorageLayoutType.Bis : StorageLayoutType.All;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountContentStorage);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenContentStorageFileSystem(ref fileSystem.Ref, contentStorageId);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));
        using var alignmentMatchableFileSystem = new SharedRef<IFileSystem>(new AlignmentMatchableFileSystem(in typeSetFileSystem));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in alignmentMatchableFileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result RegisterExternalKey(in RightsId rightsId, in AccessKey accessKey)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.RegisterExternalKey(in rightsId, in accessKey).Ret();
    }

    public Result UnregisterExternalKey(in RightsId rightsId)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.UnregisterExternalKey(in rightsId).Ret();
    }

    public Result UnregisterAllExternalKey()
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.UnregisterAllExternalKey();
    }

    public Result RegisterUpdatePartition()
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterUpdatePartition))
            return ResultFs.PermissionDenied.Log();

        ulong targetProgramId = programInfo.ProgramIdValue;

        using var programPath = new Path();
        res = _serviceImpl.ResolveRomPath(out _, ref programPath.Ref(), out ContentAttributes contentAttributes, out _,
            targetProgramId, programInfo.StorageId);
        if (res.IsFailure()) return res.Miss();

        return _serviceImpl.RegisterUpdatePartition(targetProgramId, in programPath, contentAttributes).Ret();
    }

    public Result OpenRegisteredUpdatePartition(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        const StorageLayoutType storageFlag = StorageLayoutType.All;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        Accessibility accessibility =
            programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountRegisteredUpdatePartition);
        if (!accessibility.CanRead)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenRegisteredUpdatePartition(ref fileSystem.Ref);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result IsArchivedProgram(out bool outIsArchived, ulong processId)
    {
        UnsafeHelpers.SkipParamInit(out outIsArchived);

        Result res = GetProgramInfoByProcessId(out ProgramInfo programInfo, processId);
        if (res.IsFailure()) return res.Miss();

        using var programPath = new Path();
        res = _serviceImpl.ResolveProgramPath(out _, ref programPath.Ref(), out _, programInfo.ProgramId,
            programInfo.StorageId);
        if (res.IsFailure()) return res.Miss();

        ReadOnlySpan<byte> ncaExtension = ".nca"u8;
        int ncaExtensionLength = ncaExtension.Length;

        ReadOnlySpan<byte> path = programPath.GetString();
        int pathLength = StringUtils.GetLength(path, PathTool.EntryNameLengthMax);

        if (pathLength > ncaExtensionLength &&
            StringUtils.CompareCaseInsensitive(path.Slice(pathLength - ncaExtensionLength), ".nca"u8) == 0)
        {
            outIsArchived = true;
            return Result.Success;
        }

        outIsArchived = false;
        return Result.Success;
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed encryptionSeed)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.SetEncryptionSeed))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.SetSdCardEncryptionSeed(in encryptionSeed).Ret();
    }

    public Result OpenHostFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, ref readonly FspPath path)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountHost);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        using var pathNormalized = new Path();
        res = pathNormalized.Initialize(path.Str);
        if (res.IsFailure()) return res.Miss();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenHostFileSystem(ref fileSystem.Ref, in pathNormalized, openCaseSensitive: false);
        if (res.IsFailure()) return res.Miss();

        PathFlags pathFlags = FileSystemInterfaceAdapter.GetDefaultPathFlags();
        if (path.Str.At(0) == 0)
            pathFlags.AllowWindowsPath();

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(in fileSystem, pathFlags, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenSystemDataUpdateEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.OpenSystemDataUpdateEventNotifier))
            return ResultFs.PermissionDenied.Log();

        using var systemDataUpdateEventNotifier = new UniqueRef<SystemDataUpdateEventNotifier>();
        res = _serviceImpl.CreateNotifier(ref systemDataUpdateEventNotifier.Ref);
        if (res.IsFailure()) return res.Miss();

        outEventNotifier.Set(ref systemDataUpdateEventNotifier.Ref);

        return Result.Success;
    }

    public Result NotifySystemDataUpdateEvent()
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.NotifySystemDataUpdateEvent))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.NotifySystemDataUpdateEvent().Ret();
    }

    public Result HandleResolubleAccessFailure(out bool wasDeferred, Result nonDeferredResult)
    {
        return _serviceImpl.HandleResolubleAccessFailure(out wasDeferred, nonDeferredResult, _processId).Ret();
    }

    Result IRomFileSystemAccessFailureManager.OpenDataStorageCore(ref SharedRef<IStorage> outStorage,
        ref SharedRef<IAsynchronousAccessSplitter> outStorageAccessSplitter, ref Hash outNcaDigest, ulong id,
        StorageId storageId)
    {
        return OpenDataStorageCore(ref outStorage, ref outStorageAccessSplitter, ref outNcaDigest, id, storageId).Ret();
    }
}