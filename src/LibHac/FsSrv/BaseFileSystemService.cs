using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Sf;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using Path = LibHac.Fs.Path;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv;

public readonly struct BaseFileSystemService
{
    private readonly BaseFileSystemServiceImpl _serviceImpl;
    private readonly ulong _processId;

    public BaseFileSystemService(BaseFileSystemServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
    }

    private Result GetProgramInfo(out ProgramInfo programInfo)
    {
        return GetProgramInfo(out programInfo, _processId);
    }

    private Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        return _serviceImpl.GetProgramInfo(out programInfo, processId);
    }

    private Result CheckCapabilityById(BaseFileSystemId id, ulong processId)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo, processId);
        if (rc.IsFailure()) return rc;

        if (id == BaseFileSystemId.TemporaryDirectory)
        {
            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountTemporaryDirectory);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();
        }
        else
        {
            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountAllBaseFileSystem);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();
        }

        return Result.Success;
    }

    public Result OpenBaseFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, BaseFileSystemId fileSystemId)
    {
        Result rc = CheckCapabilityById(fileSystemId, _processId);
        if (rc.IsFailure()) return rc;

        // Open the file system
        using var fileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenBaseFileSystem(ref fileSystem.Ref(), fileSystemId);
        if (rc.IsFailure()) return rc;

        // Create an SF adapter for the file system
        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref fileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result FormatBaseFileSystem(BaseFileSystemId fileSystemId)
    {
        Result rc = CheckCapabilityById(fileSystemId, _processId);
        if (rc.IsFailure()) return rc;

        return _serviceImpl.FormatBaseFileSystem(fileSystemId);
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, in FspPath rootPath,
        BisPartitionId partitionId)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        // Get the permissions the caller needs
        AccessibilityType requiredAccess = partitionId switch
        {
            BisPartitionId.CalibrationFile => AccessibilityType.MountBisCalibrationFile,
            BisPartitionId.SafeMode => AccessibilityType.MountBisSafeMode,
            BisPartitionId.User => AccessibilityType.MountBisUser,
            BisPartitionId.System => AccessibilityType.MountBisSystem,
            BisPartitionId.SystemProperPartition => AccessibilityType.MountBisSystemProperPartition,
            _ => AccessibilityType.NotMount
        };

        // Reject opening invalid partitions
        if (requiredAccess == AccessibilityType.NotMount)
            return ResultFs.InvalidArgument.Log();

        // Verify the caller has the required permissions
        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(requiredAccess);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        const StorageLayoutType storageFlag = StorageLayoutType.Bis;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        // Normalize the path
        using var pathNormalized = new Path();
        rc = pathNormalized.Initialize(rootPath.Str);
        if (rc.IsFailure()) return rc;

        var pathFlags = new PathFlags();
        pathFlags.AllowEmptyPath();
        rc = pathNormalized.Normalize(pathFlags);
        if (rc.IsFailure()) return rc;

        // Open the file system
        using var fileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenBisFileSystem(ref fileSystem.Ref(), partitionId, false);
        if (rc.IsFailure()) return rc;

        using var subDirFileSystem = new SharedRef<IFileSystem>();
        rc = Utility.CreateSubDirectoryFileSystem(ref subDirFileSystem.Ref(), ref fileSystem.Ref(),
            in pathNormalized);
        if (rc.IsFailure()) return rc;

        // Add all the file system wrappers
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref subDirFileSystem.Ref(), storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref()));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result SetBisRootForHost(BisPartitionId partitionId, in FspPath path)
    {
        throw new NotImplementedException();
    }

    public Result CreatePaddingFile(long size)
    {
        // File size must be non-negative
        if (size < 0)
            return ResultFs.InvalidSize.Log();

        // Caller must have the FillBis permission
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.CreatePaddingFile(size);
    }

    public Result DeleteAllPaddingFiles()
    {
        // Caller must have the FillBis permission
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.DeleteAllPaddingFiles();
    }

    public Result OpenGameCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountGameCard).CanRead)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();

        rc = _serviceImpl.OpenGameCardFileSystem(ref fileSystem.Ref(), handle, partitionId);
        if (rc.IsFailure()) return rc;

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref fileSystem.Ref()));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result OpenSdCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountSdCard);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        const StorageLayoutType storageFlag = StorageLayoutType.Bis;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        using var fileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenSdCardProxyFileSystem(ref fileSystem.Ref());
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

    public Result FormatSdCardFileSystem()
    {
        // Caller must have the FormatSdCard permission
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.FormatSdCard))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.FormatSdCardProxyFileSystem();
    }

    public Result FormatSdCardDryRun()
    {
        // No permissions are needed to call this method

        return _serviceImpl.FormatSdCardProxyFileSystem();
    }

    public Result IsExFatSupported(out bool isSupported)
    {
        // No permissions are needed to call this method

        isSupported = _serviceImpl.IsExFatSupported();
        return Result.Success;
    }

    public Result OpenImageDirectoryFileSystem(ref SharedRef<IFileSystemSf> outFileSystem,
        ImageDirectoryId directoryId)
    {
        // Caller must have the MountImageAndVideoStorage permission
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        Accessibility accessibility =
            programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountImageAndVideoStorage);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        // Get the base file system ID
        BaseFileSystemId fileSystemId;
        switch (directoryId)
        {
            case ImageDirectoryId.Nand:
                fileSystemId = BaseFileSystemId.ImageDirectoryNand;
                break;
            case ImageDirectoryId.SdCard:
                fileSystemId = BaseFileSystemId.ImageDirectorySdCard;
                break;
            default:
                return ResultFs.InvalidArgument.Log();
        }

        using var baseFileSystem = new SharedRef<IFileSystem>();
        rc = _serviceImpl.OpenBaseFileSystem(ref baseFileSystem.Ref(), fileSystemId);
        if (rc.IsFailure()) return rc;

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref baseFileSystem.Ref(), false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref());

        return Result.Success;
    }

    public Result OpenBisWiper(ref SharedRef<IWiper> outBisWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        // Caller must have the OpenBisWiper permission
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.OpenBisWiper))
            return ResultFs.PermissionDenied.Log();

        using var bisWiper = new UniqueRef<IWiper>();
        rc = _serviceImpl.OpenBisWiper(ref bisWiper.Ref(), transferMemoryHandle, transferMemorySize);
        if (rc.IsFailure()) return rc;

        outBisWiper.Set(ref bisWiper.Ref());

        return Result.Success;
    }
}
