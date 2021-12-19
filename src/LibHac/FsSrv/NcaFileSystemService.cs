﻿using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Lr;
using LibHac.Ncm;
using LibHac.Spl;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IStorage = LibHac.Fs.IStorage;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;
using Path = LibHac.Fs.Path;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv;

internal class NcaFileSystemService : IRomFileSystemAccessFailureManager
{
    private const int AocSemaphoreCount = 128;
    private const int RomSemaphoreCount = 10;

    private WeakRef<NcaFileSystemService> _selfReference;
    private NcaFileSystemServiceImpl _serviceImpl;
    private ulong _processId;
    private SemaphoreAdapter _aocMountCountSemaphore;
    private SemaphoreAdapter _romMountCountSemaphore;

    private NcaFileSystemService(NcaFileSystemServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
        _aocMountCountSemaphore = new SemaphoreAdapter(AocSemaphoreCount, AocSemaphoreCount);
        _romMountCountSemaphore = new SemaphoreAdapter(RomSemaphoreCount, RomSemaphoreCount);
    }

    public static SharedRef<NcaFileSystemService> CreateShared(NcaFileSystemServiceImpl serviceImpl,
        ulong processId)
    {
        // Create the service
        var ncaService = new NcaFileSystemService(serviceImpl, processId);

        // Wrap the service in a ref-counter and give the service a weak self-reference
        using var sharedService = new SharedRef<NcaFileSystemService>(ncaService);
        ncaService._selfReference.Set(in sharedService);

        return SharedRef<NcaFileSystemService>.CreateMove(ref sharedService.Ref());
    }

    public void Dispose()
    {
        _aocMountCountSemaphore?.Dispose();
        _romMountCountSemaphore?.Dispose();
        _selfReference.Destroy();
    }

    private Result GetProgramInfo(out ProgramInfo programInfo)
    {
        return _serviceImpl.GetProgramInfoByProcessId(out programInfo, _processId);
    }

    private Result GetProgramInfoByProcessId(out ProgramInfo programInfo, ulong processId)
    {
        return _serviceImpl.GetProgramInfoByProcessId(out programInfo, processId);
    }

    private Result GetProgramInfoByProgramId(out ProgramInfo programInfo, ulong programId)
    {
        return _serviceImpl.GetProgramInfoByProgramId(out programInfo, programId);
    }

    public Result OpenFileSystemWithPatch(ref SharedRef<IFileSystemSf> outFileSystem, ProgramId programId,
        FileSystemProxyType fsType)
    {
        const StorageType storageFlag = StorageType.All;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        // Get the program info for the caller and verify permissions
        Result rc = GetProgramInfo(out ProgramInfo callerProgramInfo);
        if (rc.IsFailure()) return rc;

        if (fsType != FileSystemProxyType.Manual)
        {
            switch (fsType)
            {
                case FileSystemProxyType.Logo:
                case FileSystemProxyType.Control:
                case FileSystemProxyType.Meta:
                case FileSystemProxyType.Data:
                case FileSystemProxyType.Package:
                    return ResultFs.NotImplemented.Log();
                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        Accessibility accessibility =
            callerProgramInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountContentManual);

        if (!accessibility.CanRead)
            return ResultFs.PermissionDenied.Log();

        // Get the program info for the owner of the file system being opened
        rc = GetProgramInfoByProgramId(out ProgramInfo ownerProgramInfo, programId.Value);
        if (rc.IsFailure()) return rc;

        // Try to find the path to the original version of the file system
        using var originalPath = new Path();
        Result originalResult = _serviceImpl.ResolveApplicationHtmlDocumentPath(out bool isDirectory,
            ref originalPath.Ref(), new Ncm.ApplicationId(programId.Value), ownerProgramInfo.StorageId);

        // The file system might have a patch version with no original version, so continue if not found
        if (originalResult.IsFailure() && !ResultLr.HtmlDocumentNotFound.Includes(originalResult))
            return originalResult;

        // Try to find the path to the patch file system
        using var patchPath = new Path();
        Result patchResult = _serviceImpl.ResolveRegisteredHtmlDocumentPath(ref patchPath.Ref(), programId.Value);

        using var fileSystem = new SharedRef<IFileSystem>();

        if (ResultLr.HtmlDocumentNotFound.Includes(patchResult))
        {
            // There must either be an original version or patch version of the file system being opened
            if (originalResult.IsFailure())
                return originalResult;

            // There is an original version and no patch version. Open the original directly
            rc = _serviceImpl.OpenFileSystem(ref fileSystem.Ref(), in originalPath, fsType, programId.Value,
                isDirectory);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            if (patchResult.IsFailure())
                return patchResult;

            ref readonly Path originalNcaPath = ref originalResult.IsSuccess()
                ? ref originalPath
                : ref PathExtensions.GetNullRef();

            // Open the file system using both the original and patch versions
            rc = _serviceImpl.OpenFileSystemWithPatch(ref fileSystem.Ref(), in originalNcaPath, in patchPath,
                fsType, programId.Value);
            if (rc.IsFailure()) return rc;
        }

        // Add all the file system wrappers
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref(), storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref()));

        using SharedRef<IRomFileSystemAccessFailureManager> accessFailureManager =
            SharedRef<IRomFileSystemAccessFailureManager>.Create(in _selfReference);

        using SharedRef<IFileSystem> retryFileSystem =
            DeepRetryFileSystem.CreateShared(ref asyncFileSystem.Ref(), ref accessFailureManager.Ref());

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref retryFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result OpenCodeFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        out CodeVerificationData verificationData, in FspPath path, ProgramId programId)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataFileSystemByCurrentProcess(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        throw new NotImplementedException();
    }

    private Result TryAcquireAddOnContentOpenCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphoreLock)
    {
        throw new NotImplementedException();
    }

    private Result TryAcquireRomMountCountSemaphore(ref UniqueRef<IUniqueLock> outSemaphoreLock)
    {
        throw new NotImplementedException();
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

    private Result OpenDataStorageCore(ref SharedRef<IStorage> outStorage, out Hash ncaHeaderDigest,
        ulong id, StorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataStorageByCurrentProcess(ref SharedRef<IStorageSf> outStorage)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataStorageByProgramId(ref SharedRef<IStorageSf> outStorage, ProgramId programId)
    {
        throw new NotImplementedException();
    }

    public Result OpenFileSystemWithId(ref SharedRef<IFileSystemSf> outFileSystem, in FspPath path,
        ulong id, FileSystemProxyType fsType)
    {
        const StorageType storageFlag = StorageType.All;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        AccessControl ac = programInfo.AccessControl;

        switch (fsType)
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

        if (fsType == FileSystemProxyType.Meta)
        {
            id = ulong.MaxValue;
        }
        else if (id == ulong.MaxValue)
        {
            return ResultFs.InvalidArgument.Log();
        }

        bool canMountSystemDataPrivate = ac.GetAccessibilityFor(AccessibilityType.MountSystemDataPrivate).CanRead;

        using var pathNormalized = new Path();
        rc = pathNormalized.InitializeWithReplaceUnc(path.Str);
        if (rc.IsFailure()) return rc;

        var pathFlags = new PathFlags();
        pathFlags.AllowWindowsPath();
        pathFlags.AllowMountName();
        rc = pathNormalized.Normalize(pathFlags);
        if (rc.IsFailure()) return rc;

        bool isDirectory = PathUtility.IsDirectoryPath(in path);

        using var fileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenFileSystem(ref fileSystem.Ref(), in pathNormalized, fsType, canMountSystemDataPrivate,
            id, isDirectory);
        if (rc.IsFailure()) return rc;

        // Add all the wrappers for the file system
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref(), storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref fileSystem.Ref()));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result OpenDataFileSystemByProgramId(ref SharedRef<IFileSystemSf> outFileSystem, ProgramId programId)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataStorageByDataId(ref SharedRef<IStorageSf> outStorage, DataId dataId, StorageId storageId)
    {
        throw new NotImplementedException();
    }

    public Result OpenDataFileSystemWithProgramIndex(ref SharedRef<IFileSystemSf> outFileSystem, byte programIndex)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        // Get the program ID of the program with the specified index if in a multi-program application
        rc = _serviceImpl.ResolveRomReferenceProgramId(out ProgramId targetProgramId, programInfo.ProgramId,
            programIndex);
        if (rc.IsFailure()) return rc;

        using var fileSystem = new SharedRef<IFileSystem>();
        rc = OpenDataFileSystemCore(ref fileSystem.Ref(), out _, targetProgramId.Value, programInfo.StorageId);
        if (rc.IsFailure()) return rc;

        // Verify the caller has access to the file system
        if (targetProgramId != programInfo.ProgramId &&
            !programInfo.AccessControl.HasContentOwnerId(targetProgramId.Value))
        {
            return ResultFs.PermissionDenied.Log();
        }

        // Add all the file system wrappers
        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref fileSystem.Ref()));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result OpenDataStorageWithProgramIndex(ref SharedRef<IStorageSf> outStorage, byte programIndex)
    {
        throw new NotImplementedException();
    }

    public Result GetRightsId(out RightsId outRightsId, ProgramId programId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outRightsId);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.All);

        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.GetRightsId))
            return ResultFs.PermissionDenied.Log();

        using var programPath = new Path();
        rc = _serviceImpl.ResolveProgramPath(out bool isDirectory, ref programPath.Ref(), programId, storageId);
        if (rc.IsFailure()) return rc;

        if (isDirectory)
            return ResultFs.TargetNotFound.Log();

        rc = _serviceImpl.GetRightsId(out RightsId rightsId, out _, in programPath, programId);
        if (rc.IsFailure()) return rc;

        outRightsId = rightsId;

        return Result.Success;
    }

    public Result GetRightsIdAndKeyGenerationByPath(out RightsId outRightsId, out byte outKeyGeneration, in FspPath path)
    {
        const ulong checkThroughProgramId = ulong.MaxValue;
        UnsafeHelpers.SkipParamInit(out outRightsId, out outKeyGeneration);

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageType.All);

        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.GetRightsId))
            return ResultFs.PermissionDenied.Log();

        using var pathNormalized = new Path();
        rc = pathNormalized.Initialize(path.Str);
        if (rc.IsFailure()) return rc;

        var pathFlags = new PathFlags();
        pathFlags.AllowWindowsPath();
        pathFlags.AllowMountName();
        rc = pathNormalized.Normalize(pathFlags);
        if (rc.IsFailure()) return rc;

        if (PathUtility.IsDirectoryPath(in path))
            return ResultFs.TargetNotFound.Log();

        rc = _serviceImpl.GetRightsId(out RightsId rightsId, out byte keyGeneration, in pathNormalized,
            new ProgramId(checkThroughProgramId));
        if (rc.IsFailure()) return rc;

        outRightsId = rightsId;
        outKeyGeneration = keyGeneration;

        return Result.Success;
    }

    // ReSharper disable once OutParameterValueIsAlwaysDiscarded.Local
    private Result OpenDataFileSystemCore(ref SharedRef<IFileSystem> outFileSystem, out bool isHostFs,
        ulong programId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out isHostFs);

        StorageType storageFlag = _serviceImpl.GetStorageFlag(programId);
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        using var programPath = new Path();
        Result rc = _serviceImpl.ResolveRomPath(out bool isDirectory, ref programPath.Ref(), programId, storageId);
        if (rc.IsFailure()) return rc;

        isHostFs = Utility.IsHostFsMountName(programPath.GetString());

        using var fileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenDataFileSystem(ref fileSystem.Ref(), in programPath, FileSystemProxyType.Rom,
            programId, isDirectory);
        if (rc.IsFailure()) return rc;

        // Add all the file system wrappers
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref(), storageFlag));

        outFileSystem.SetByMove(ref typeSetFileSystem.Ref());

        return Result.Success;
    }

    public Result OpenContentStorageFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        ContentStorageId contentStorageId)
    {
        StorageType storageFlag = contentStorageId == ContentStorageId.System ? StorageType.Bis : StorageType.All;
        using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        Accessibility accessibility =
            programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountContentStorage);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenContentStorageFileSystem(ref fileSystem.Ref(), contentStorageId);
        if (rc.IsFailure()) return rc;

        // Add all the file system wrappers
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref(), storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref()));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result RegisterExternalKey(in RightsId rightsId, in AccessKey accessKey)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.RegisterExternalKey(in rightsId, in accessKey);
    }

    public Result UnregisterExternalKey(in RightsId rightsId)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.UnregisterExternalKey(in rightsId);
    }

    public Result UnregisterAllExternalKey()
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterExternalKey))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.UnregisterAllExternalKey();
    }

    public Result RegisterUpdatePartition()
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.RegisterUpdatePartition))
            return ResultFs.PermissionDenied.Log();

        ulong targetProgramId = programInfo.ProgramIdValue;

        using var programPath = new Path();
        rc = _serviceImpl.ResolveRomPath(out _, ref programPath.Ref(), targetProgramId, programInfo.StorageId);
        if (rc.IsFailure()) return rc;

        return _serviceImpl.RegisterUpdatePartition(targetProgramId, in programPath);
    }

    public Result OpenRegisteredUpdatePartition(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        var storageFlag = StorageType.All;
        using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        Accessibility accessibility =
            programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountRegisteredUpdatePartition);
        if (!accessibility.CanRead)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenRegisteredUpdatePartition(ref fileSystem.Ref());
        if (rc.IsFailure()) return rc;

        // Add all the file system wrappers
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref fileSystem.Ref(), storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref()));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result IsArchivedProgram(out bool isArchived, ulong processId)
    {
        throw new NotImplementedException();
    }

    public Result SetSdCardEncryptionSeed(in EncryptionSeed encryptionSeed)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.SetEncryptionSeed))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.SetSdCardEncryptionSeed(in encryptionSeed);
    }

    public Result OpenSystemDataUpdateEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier)
    {
        throw new NotImplementedException();
    }

    public Result NotifySystemDataUpdateEvent()
    {
        throw new NotImplementedException();
    }

    public Result HandleResolubleAccessFailure(out bool wasDeferred, Result resultForNoFailureDetected)
    {
        return _serviceImpl.HandleResolubleAccessFailure(out wasDeferred, resultForNoFailureDetected, _processId);
    }

    Result IRomFileSystemAccessFailureManager.OpenDataStorageCore(ref SharedRef<IStorage> outStorage,
        out Hash ncaHeaderDigest, ulong id, StorageId storageId)
    {
        return OpenDataStorageCore(ref outStorage, out ncaHeaderDigest, id, storageId);
    }
}