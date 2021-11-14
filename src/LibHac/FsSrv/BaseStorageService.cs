using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using IStorage = LibHac.Fs.IStorage;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv;

public readonly struct BaseStorageService
{
    private readonly BaseStorageServiceImpl _serviceImpl;
    private readonly ulong _processId;

    public BaseStorageService(BaseStorageServiceImpl serviceImpl, ulong processId)
    {
        _serviceImpl = serviceImpl;
        _processId = processId;
    }

    private Result GetProgramInfo(out ProgramInfo programInfo)
    {
        return _serviceImpl.GetProgramInfo(out programInfo, _processId);
    }

    private static Result GetAccessibilityForOpenBisPartition(out Accessibility accessibility, ProgramInfo programInfo,
        BisPartitionId partitionId)
    {
        UnsafeHelpers.SkipParamInit(out accessibility);

        AccessibilityType type = partitionId switch
        {
            BisPartitionId.BootPartition1Root => AccessibilityType.OpenBisPartitionBootPartition1Root,
            BisPartitionId.BootPartition2Root => AccessibilityType.OpenBisPartitionBootPartition2Root,
            BisPartitionId.UserDataRoot => AccessibilityType.OpenBisPartitionUserDataRoot,
            BisPartitionId.BootConfigAndPackage2Part1 => AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part1,
            BisPartitionId.BootConfigAndPackage2Part2 => AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part2,
            BisPartitionId.BootConfigAndPackage2Part3 => AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part3,
            BisPartitionId.BootConfigAndPackage2Part4 => AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part4,
            BisPartitionId.BootConfigAndPackage2Part5 => AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part5,
            BisPartitionId.BootConfigAndPackage2Part6 => AccessibilityType.OpenBisPartitionBootConfigAndPackage2Part6,
            BisPartitionId.CalibrationBinary => AccessibilityType.OpenBisPartitionCalibrationBinary,
            BisPartitionId.CalibrationFile => AccessibilityType.OpenBisPartitionCalibrationFile,
            BisPartitionId.SafeMode => AccessibilityType.OpenBisPartitionSafeMode,
            BisPartitionId.User => AccessibilityType.OpenBisPartitionUser,
            BisPartitionId.System => AccessibilityType.OpenBisPartitionSystem,
            BisPartitionId.SystemProperEncryption => AccessibilityType.OpenBisPartitionSystemProperEncryption,
            BisPartitionId.SystemProperPartition => AccessibilityType.OpenBisPartitionSystemProperPartition,
            _ => (AccessibilityType)(-1)
        };

        if (type == (AccessibilityType)(-1))
            return ResultFs.InvalidArgument.Log();

        accessibility = programInfo.AccessControl.GetAccessibilityFor(type);
        return Result.Success;
    }

    public Result OpenBisStorage(ref SharedRef<IStorageSf> outStorage, BisPartitionId id)
    {
        var storageFlag = StorageType.Bis;
        using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        rc = GetAccessibilityForOpenBisPartition(out Accessibility accessibility, programInfo, id);
        if (rc.IsFailure()) return rc;

        bool canAccess = accessibility.CanRead && accessibility.CanWrite;

        if (!canAccess)
            return ResultFs.PermissionDenied.Log();

        using var storage = new SharedRef<IStorage>();
        rc = _serviceImpl.OpenBisStorage(ref storage.Ref(), id);
        if (rc.IsFailure()) return rc;

        using var typeSetStorage =
            new SharedRef<IStorage>(new StorageLayoutTypeSetStorage(ref storage.Ref(), storageFlag));

        // Todo: Async storage

        using var storageAdapter =
            new SharedRef<IStorageSf>(new StorageInterfaceAdapter(ref typeSetStorage.Ref()));

        outStorage.SetByMove(ref storageAdapter.Ref());

        return Result.Success;
    }

    public Result InvalidateBisCache()
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.InvalidateBisCache))
            return ResultFs.PermissionDenied.Log();

        return _serviceImpl.InvalidateBisCache();
    }

    public Result OpenGameCardStorage(ref SharedRef<IStorageSf> outStorage, GameCardHandle handle,
        GameCardPartitionRaw partitionId)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        Accessibility accessibility =
            programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

        bool canAccess = accessibility.CanRead && accessibility.CanWrite;

        if (!canAccess)
            return ResultFs.PermissionDenied.Log();

        using var storage = new SharedRef<IStorage>();
        rc = _serviceImpl.OpenGameCardPartition(ref storage.Ref(), handle, partitionId);
        if (rc.IsFailure()) return rc;

        // Todo: Async storage

        using var storageAdapter =
            new SharedRef<IStorageSf>(new StorageInterfaceAdapter(ref storage.Ref()));

        outStorage.SetByMove(ref storageAdapter.Ref());

        return Result.Success;
    }

    public Result OpenDeviceOperator(ref SharedRef<IDeviceOperator> outDeviceOperator)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc.Miss();

        rc = _serviceImpl.OpenDeviceOperator(ref outDeviceOperator, programInfo.AccessControl);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public Result OpenSdCardDetectionEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.OpenSdCardDetectionEventNotifier))
            return ResultFs.PermissionDenied.Log();

        throw new NotImplementedException();
    }

    public Result OpenGameCardDetectionEventNotifier(ref SharedRef<IEventNotifier> outEventNotifier)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.OpenGameCardDetectionEventNotifier))
            return ResultFs.PermissionDenied.Log();

        throw new NotImplementedException();
    }

    public Result SimulateDeviceDetectionEvent(SdmmcPort port, SimulatingDeviceDetectionMode mode, bool signalEvent)
    {
        Result rc = GetProgramInfo(out ProgramInfo programInfo);
        if (rc.IsFailure()) return rc;

        if (!programInfo.AccessControl.CanCall(OperationType.SimulateDevice))
            return ResultFs.PermissionDenied.Log();

        throw new NotImplementedException();
    }
}

public class BaseStorageServiceImpl
{
    private Configuration _config;

    // LibHac addition
    private SharedRef<IDeviceOperator> _deviceOperator;

    public BaseStorageServiceImpl(in Configuration configuration)
    {
        _config = configuration;
        _deviceOperator = new SharedRef<IDeviceOperator>(configuration.DeviceOperator);
    }

    public struct Configuration
    {
        public IBuiltInStorageCreator BisStorageCreator;
        public IGameCardStorageCreator GameCardStorageCreator;

        // LibHac additions
        public FileSystemServer FsServer;
        // Todo: The DeviceOperator in FS uses mostly global state. Decide how to handle this.
        public IDeviceOperator DeviceOperator;
    }

    internal Result GetProgramInfo(out ProgramInfo programInfo, ulong processId)
    {
        var registry = new ProgramRegistryImpl(_config.FsServer);
        return registry.GetProgramInfo(out programInfo, processId);
    }

    public Result OpenBisStorage(ref SharedRef<IStorage> outStorage, BisPartitionId partitionId)
    {
        return _config.BisStorageCreator.Create(ref outStorage, partitionId);
    }

    public Result InvalidateBisCache()
    {
        return _config.BisStorageCreator.InvalidateCache();
    }

    public Result OpenGameCardPartition(ref SharedRef<IStorage> outStorage, GameCardHandle handle,
        GameCardPartitionRaw partitionId)
    {
        switch (partitionId)
        {
            case GameCardPartitionRaw.NormalReadOnly:
                return _config.GameCardStorageCreator.CreateReadOnly(handle, ref outStorage);
            case GameCardPartitionRaw.SecureReadOnly:
                return _config.GameCardStorageCreator.CreateSecureReadOnly(handle, ref outStorage);
            case GameCardPartitionRaw.RootWriteOnly:
                return _config.GameCardStorageCreator.CreateWriteOnly(handle, ref outStorage);
            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    // LibHac addition
    internal Result OpenDeviceOperator(ref SharedRef<IDeviceOperator> outDeviceOperator,
        AccessControl accessControl)
    {
        outDeviceOperator.SetByCopy(in _deviceOperator);
        return Result.Success;
    }
}
