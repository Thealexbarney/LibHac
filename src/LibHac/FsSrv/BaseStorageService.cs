using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.FsCreator;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSystem;
using IStorage = LibHac.Fs.IStorage;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv
{
    public readonly struct BaseStorageService
    {
        private readonly BaseStorageServiceImpl _serviceImpl;
        private readonly ulong _processId;

        public BaseStorageService(BaseStorageServiceImpl serviceImpl, ulong processId)
        {
            _serviceImpl = serviceImpl;
            _processId = processId;
        }

        public Result OpenBisStorage(out ReferenceCountedDisposable<IStorageSf> storage, BisPartitionId id)
        {
            UnsafeHelpers.SkipParamInit(out storage);

            var storageFlag = StorageType.Bis;
            using var scopedLayoutType = new ScopedStorageLayoutTypeSetter(storageFlag);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            rc = GetAccessibilityForOpenBisPartition(out Accessibility accessibility, programInfo, id);
            if (rc.IsFailure()) return rc;

            bool canAccess = accessibility.CanRead && accessibility.CanWrite;

            if (!canAccess)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IStorage> tempStorage = null;
            try
            {
                rc = _serviceImpl.OpenBisStorage(out tempStorage, id);
                if (rc.IsFailure()) return rc;

                tempStorage = StorageLayoutTypeSetStorage.CreateShared(ref tempStorage, storageFlag);

                // Todo: Async storage

                storage = StorageInterfaceAdapter.CreateShared(ref tempStorage);
                return Result.Success;

            }
            finally
            {
                tempStorage?.Dispose();
            }
        }

        public Result InvalidateBisCache()
        {
            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.InvalidateBisCache))
                return ResultFs.PermissionDenied.Log();

            return _serviceImpl.InvalidateBisCache();
        }

        public Result OpenGameCardStorage(out ReferenceCountedDisposable<IStorageSf> storage, GameCardHandle handle,
            GameCardPartitionRaw partitionId)
        {
            UnsafeHelpers.SkipParamInit(out storage);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            Accessibility accessibility =
                programInfo.AccessControl.GetAccessibilityFor(AccessibilityType.OpenGameCardStorage);

            bool canAccess = accessibility.CanRead && accessibility.CanWrite;

            if (!canAccess)
                return ResultFs.PermissionDenied.Log();

            ReferenceCountedDisposable<IStorage> tempStorage = null;
            try
            {
                rc = _serviceImpl.OpenGameCardPartition(out tempStorage, handle, partitionId);
                if (rc.IsFailure()) return rc;

                // Todo: Async storage

                storage = StorageInterfaceAdapter.CreateShared(ref tempStorage);
                return Result.Success;
            }
            finally
            {
                tempStorage?.Dispose();
            }
        }

        public Result OpenDeviceOperator(out ReferenceCountedDisposable<IDeviceOperator> deviceOperator)
        {
            deviceOperator = _serviceImpl.Config.DeviceOperator.AddReference();
            return Result.Success;
        }

        public Result OpenSdCardDetectionEventNotifier(out ReferenceCountedDisposable<IEventNotifier> eventNotifier)
        {
            UnsafeHelpers.SkipParamInit(out eventNotifier);

            Result rc = GetProgramInfo(out ProgramInfo programInfo);
            if (rc.IsFailure()) return rc;

            if (!programInfo.AccessControl.CanCall(OperationType.OpenSdCardDetectionEventNotifier))
                return ResultFs.PermissionDenied.Log();

            throw new NotImplementedException();
        }

        public Result OpenGameCardDetectionEventNotifier(out ReferenceCountedDisposable<IEventNotifier> eventNotifier)
        {
            UnsafeHelpers.SkipParamInit(out eventNotifier);

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

        private Result GetProgramInfo(out ProgramInfo programInfo)
        {
            var registry = new ProgramRegistryImpl(_serviceImpl.Config.FsServer);
            return registry.GetProgramInfo(out programInfo, _processId);
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
    }

    public class BaseStorageServiceImpl
    {
        internal Configuration Config;

        public BaseStorageServiceImpl(in Configuration configuration)
        {
            Config = configuration;
        }

        public struct Configuration
        {
            public IBuiltInStorageCreator BisStorageCreator;
            public IGameCardStorageCreator GameCardStorageCreator;

            // LibHac additions
            public FileSystemServer FsServer;
            // Todo: The DeviceOperator in FS uses mostly global state. Decide how to handle this.
            public ReferenceCountedDisposable<IDeviceOperator> DeviceOperator;
        }

        public Result OpenBisStorage(out ReferenceCountedDisposable<IStorage> storage, BisPartitionId partitionId)
        {
            return Config.BisStorageCreator.Create(out storage, partitionId);
        }

        public Result InvalidateBisCache()
        {
            return Config.BisStorageCreator.InvalidateCache();
        }

        public Result OpenGameCardPartition(out ReferenceCountedDisposable<IStorage> storage, GameCardHandle handle,
            GameCardPartitionRaw partitionId)
        {
            switch (partitionId)
            {
                case GameCardPartitionRaw.NormalReadOnly:
                    return Config.GameCardStorageCreator.CreateReadOnly(handle, out storage);
                case GameCardPartitionRaw.SecureReadOnly:
                    return Config.GameCardStorageCreator.CreateSecureReadOnly(handle, out storage);
                case GameCardPartitionRaw.RootWriteOnly:
                    return Config.GameCardStorageCreator.CreateWriteOnly(handle, out storage);
                default:
                    UnsafeHelpers.SkipParamInit(out storage);
                    return ResultFs.InvalidArgument.Log();
            }
        }
    }
}
