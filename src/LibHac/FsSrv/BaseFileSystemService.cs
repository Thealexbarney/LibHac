using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using LibHac.Sf;
using static LibHac.FsSrv.Anonymous;
using IFileSystem = LibHac.Fs.Fsa.IFileSystem;
using IFileSystemSf = LibHac.FsSrv.Sf.IFileSystem;
using Path = LibHac.Fs.Path;
using Utility = LibHac.FsSrv.Impl.Utility;

namespace LibHac.FsSrv;

file static class Anonymous
{
    public static Result GetProgramInfo(FileSystemServer fsServer, out ProgramInfo programInfo, ulong processId)
    {
        var programRegistry = new ProgramRegistryImpl(fsServer);
        return programRegistry.GetProgramInfo(out programInfo, processId).Ret();
    }

    public static Result CheckCapabilityById(FileSystemServer fsServer, BaseFileSystemId id, ulong processId)
    {
        Result res = GetProgramInfo(fsServer, out ProgramInfo programInfo, processId);
        if (res.IsFailure()) return res.Miss();

        AccessControl accessControl = programInfo.AccessControl;

        if (id == BaseFileSystemId.TemporaryDirectory)
        {
            Accessibility accessibility = accessControl.GetAccessibilityFor(AccessibilityType.MountTemporaryDirectory);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();
        }
        else
        {
            Accessibility accessibility = accessControl.GetAccessibilityFor(AccessibilityType.MountAllBaseFileSystem);

            if (!accessibility.CanRead || !accessibility.CanWrite)
                return ResultFs.PermissionDenied.Log();
        }

        return Result.Success;
    }
}

/// <summary>
/// Handles managing and opening file systems that aren't NCAs or save data. 
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
public readonly struct BaseFileSystemService
{
    private readonly BaseFileSystemServiceImpl _serviceImpl;
    private readonly ulong _processId;

    private FileSystemServer FsServer => _serviceImpl.FsServer;

    public BaseFileSystemService(BaseFileSystemServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
    }

    public Result OpenBaseFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, BaseFileSystemId fileSystemId)
    {
        Result res = CheckCapabilityById(FsServer, fileSystemId, _processId);
        if (res.IsFailure()) return res.Miss();

        // Open the file system
        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenBaseFileSystem(ref fileSystem.Ref, fileSystemId);
        if (res.IsFailure()) return res.Miss();

        // Create an SF adapter for the file system
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in fileSystem, allowAllOperations: false);
        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result FormatBaseFileSystem(BaseFileSystemId fileSystemId)
    {
        Result res = CheckCapabilityById(FsServer, fileSystemId, _processId);
        if (res.IsFailure()) return res.Miss();

        return _serviceImpl.FormatBaseFileSystem(fileSystemId).Ret();
    }

    public Result OpenBisFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, ref readonly FspPath rootPath,
        BisPartitionId partitionId)
    {
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        // Verify the caller has the required permissions
        switch (partitionId)
        {
            case BisPartitionId.CalibrationFile:
            {
                Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountBisCalibrationFile);
                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();

                break;
            }
            case BisPartitionId.SafeMode:
            {
                Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountBisSafeMode);
                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();

                break;
            }
            case BisPartitionId.System:
            {
                Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountBisSystem);
                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();

                break;
            }
            case BisPartitionId.System0:
            {
                Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountBisSystem);
                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();

                break;
            }
            case BisPartitionId.User:
            {
                Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountBisUser);
                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();

                break;
            }
            case BisPartitionId.SystemProperPartition:
            {
                Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountBisSystemProperPartition);
                if (!accessibility.CanRead || !accessibility.CanWrite)
                    return ResultFs.PermissionDenied.Log();

                break;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }

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
        res = _serviceImpl.OpenBisFileSystem(ref fileSystem.Ref, partitionId, caseSensitive: false);
        if (res.IsFailure()) return res.Miss();

        using var subDirFileSystem = new SharedRef<IFileSystem>();
        res = Utility.CreateSubDirectoryFileSystem(ref subDirFileSystem.Ref, in fileSystem, in pathNormalized);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in subDirFileSystem, storageFlag));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result CreatePaddingFile(long size)
    {
        // File size must be non-negative
        if (size < 0)
            return ResultFs.InvalidSize.Log();

        // Caller must have the FillBis permission
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
            return ResultFs.PermissionDenied.Log();

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);

        return _serviceImpl.CreatePaddingFile(size).Ret();
    }

    public Result DeleteAllPaddingFiles()
    {
        // Caller must have the FillBis permission
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.FillBis))
            return ResultFs.PermissionDenied.Log();

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.Bis);

        return _serviceImpl.DeleteAllPaddingFiles().Ret();
    }

    public Result OpenGameCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, GameCardHandle handle,
        GameCardPartition partitionId)
    {
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountGameCard).CanRead)
            return ResultFs.PermissionDenied.Log();

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenGameCardFileSystem(ref fileSystem.Ref, handle, partitionId);
        if (res.IsFailure()) return res.Miss();

        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in fileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenSdCardFileSystem(ref SharedRef<IFileSystemSf> outFileSystem)
    {
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        Accessibility accessibility = programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.MountSdCard);

        if (!accessibility.CanRead || !accessibility.CanWrite)
            return ResultFs.PermissionDenied.Log();

        const StorageLayoutType storageFlag = StorageLayoutType.SdCard;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        using var fileSystem = new SharedRef<IFileSystem>();
        res = _serviceImpl.OpenSdCardProxyFileSystem(ref fileSystem.Ref);
        if (res.IsFailure()) return res.Miss();

        // Add all the file system wrappers
        using var typeSetFileSystem = new SharedRef<IFileSystem>(new StorageLayoutTypeSetFileSystem(in fileSystem, storageFlag));
        using var asyncFileSystem = new SharedRef<IFileSystem>(new AsynchronousAccessFileSystem(in typeSetFileSystem));
        using SharedRef<IFileSystemSf> fileSystemAdapter = FileSystemInterfaceAdapter.CreateShared(in asyncFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result FormatSdCardFileSystem()
    {
        // Caller must have the FormatSdCard permission
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
        if (res.IsFailure()) return res.Miss();

        if (!programInfo.AccessControl.CanCall(OperationType.FormatSdCard))
            return ResultFs.PermissionDenied.Log();

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.SdCard);

        return _serviceImpl.FormatSdCardProxyFileSystem().Ret();
    }

    public Result FormatSdCardDryRun()
    {
        // No permissions are needed to call this method

        using var scopedContext = new ScopedStorageLayoutTypeSetter(StorageLayoutType.SdCard);

        return _serviceImpl.FormatSdCardDryRun().Ret();
    }

    public Result IsExFatSupported(out bool isSupported)
    {
        // No permissions are needed to call this method

        isSupported = _serviceImpl.IsExFatSupported();
        return Result.Success;
    }

    public Result OpenImageDirectoryFileSystem(ref SharedRef<IFileSystemSf> outFileSystem, ImageDirectoryId directoryId)
    {
        const StorageLayoutType storageFlag = StorageLayoutType.NonGameCard;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        // Caller must have the MountImageAndVideoStorage permission
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
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
            FileSystemInterfaceAdapter.CreateShared(in baseFileSystem, allowAllOperations: false);

        outFileSystem.SetByMove(ref fileSystemAdapter.Ref);

        return Result.Success;
    }

    public Result OpenBisWiper(ref SharedRef<IWiper> outBisWiper, NativeHandle transferMemoryHandle,
        ulong transferMemorySize)
    {
        const StorageLayoutType storageFlag = StorageLayoutType.Bis;
        using var scopedContext = new ScopedStorageLayoutTypeSetter(storageFlag);

        // Caller must have the OpenBisWiper permission
        Result res = GetProgramInfo(FsServer, out ProgramInfo programInfo, _processId);
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