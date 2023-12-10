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
        Result res = GetProgramInfo(out ProgramInfo programInfo, processId);
        if (res.IsFailure()) return res.Miss();

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
        Result res = CheckCapabilityById(fileSystemId, _processId);
        if (res.IsFailure()) return res.Miss();

        // Open the file system
        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenBaseFileSystem(ref fileSystem.Ref, fileSystemId);
        if (res.IsFailure()) return res.Miss();

        // Create an SF adapter for the file system
        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref fileSystem.Ref, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result FormatBaseFileSystem(BaseFileSystemId fileSystemId)
    {
        Result res = CheckCapabilityById(fileSystemId, _processId);
        if (res.IsFailure()) return res.Miss();

        return _serviceImpl.FormatBaseFileSystem(fileSystemId);
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, ref readonly FspPath rootPath,
        BisPartitionId partitionId)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

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
        res = pathNormalized.Initialize(rootPath.Str);
        if (res.IsFailure()) return res.Miss();

        var pathFlags = new PathFlags();
        pathFlags.AllowEmptyPath();
        res = pathNormalized.Normalize(pathFlags);
        if (res.IsFailure()) return res.Miss();

        // Open the file system
        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenBisFileSystem(ref fileSystem.Ref, partitionId, false);
        if (res.IsFailure()) return res.Miss();

        using var subDirFileSystem = new SharedRef<IFileSystem>();
        res = Utility.CreateSubDirectoryFileSystem(ref subDirFileSystem.Ref, ref fileSystem.Ref,
            in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem =
            new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(ref subDirFileSystem.Ref, storageFlag));

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref typeSetFileSystem.Ref));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result SetBisRootForHost(BisPartitionId partitionId, ref readonly FspPath path)
    {
        throw new NotImplementedException();
    }

    public Result CreatePaddingFile(long size)
    {
        // File size must be non-negative
        if (size < 0)
            return ResultFs.InvalidSize.Log();

        // Caller must have the FillBis permission
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.CreatePaddingFile(size);
    }

    public Result DeleteAllPaddingFiles()
    {
        // Caller must have the FillBis permission
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.DeleteAllPaddingFiles();
    }

    public Result OpenGameCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountGameCard).CanRead)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();

        res = _serviceImpl.OpenGameCardFileSystem(ref fileSystem.Ref, handle, partitionId);
        if (res.IsFailure()) return res.Miss();

        using var asyncFileSystem =
            new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(ref fileSystem.Ref));

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref asyncFileSystem.Ref, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenSdCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountSdCard);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        const StorageLayoutType storageFlag = StorageLayoutType.Bis;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenSdCardProxyFileSystem(ref fileSystem.Ref);
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

    public Result FormatSdCardFileSystem()
    {
        // Caller must have the FormatSdCard permission
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

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
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

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
        res = _serviceImpl.OpenBaseFileSystem(ref baseFileSystem.Ref, fileSystemId);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IFileSystemSf> fileSystemAdapter =
            FileSystemInterfaceAdapter.CreateShared(ref baseFileSystem.Ref, false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenBisWiper(ref SharedRef<IWiper> outBisWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        // Caller must have the OpenBisWiper permission
        Result res = GetProgramInfo(out ProgramInfo programInfo);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.OpenBisWiper))
            return ResultFs.PermissionDenied.Log();

        using var bisWiper = new UniqueRef<IWiper>();
        res = _serviceImpl.OpenBisWiper(ref bisWiper.Ref, transferMemoryHandle, transferMemorySize);
        if (res.IsFailure()) return res.Miss();

        outBisWiper.Set(ref bisWiper.Ref);

        return Result.Success;
    }
}