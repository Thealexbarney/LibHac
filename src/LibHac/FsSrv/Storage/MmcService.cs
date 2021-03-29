using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sdmmc;
using LibHac.SdmmcSrv;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Storage
{
    internal static class MmcService
    {
        private static Result GetMmcManager(this StorageService service,
            out ReferenceCountedDisposable<IStorageDeviceManager> manager)
        {
            return service.CreateStorageDeviceManager(out manager, StorageDevicePortId.Mmc);
        }

        private static Result GetMmcManagerOperator(this StorageService service,
            out ReferenceCountedDisposable<IStorageDeviceOperator> deviceOperator)
        {
            UnsafeHelpers.SkipParamInit(out deviceOperator);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetMmcManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                return deviceManager.Target.OpenOperator(out deviceOperator);
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        private static Result GetAttribute(out ulong attribute, MmcPartition partition)
        {
            UnsafeHelpers.SkipParamInit(out attribute);

            switch (partition)
            {
                case MmcPartition.UserData:
                    attribute = 0;
                    return Result.Success;
                case MmcPartition.BootPartition1:
                    attribute = 1;
                    return Result.Success;
                case MmcPartition.BootPartition2:
                    attribute = 2;
                    return Result.Success;
                default:
                    return ResultFs.InvalidArgument.Log();
            }
        }

        private static bool IsSpeedEmulationNeeded(MmcPartition partition)
        {
            return partition == MmcPartition.UserData;
        }

        private static Result GetMmcOperator(this StorageService service,
            out ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator, MmcPartition partition)
        {
            UnsafeHelpers.SkipParamInit(out mmcOperator);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetMmcManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                rc = GetAttribute(out ulong attribute, partition);
                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<IStorageDevice> storageDevice = null;
                try
                {
                    rc = deviceManager.Target.OpenDevice(out storageDevice, attribute);
                    if (rc.IsFailure()) return rc;

                    return storageDevice.Target.OpenOperator(out mmcOperator);
                }
                finally
                {
                    storageDevice?.Dispose();
                }
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        private static int MakeOperationId(MmcManagerOperationIdValue operation)
        {
            return (int)operation;
        }

        private static int MakeOperationId(MmcOperationIdValue operation)
        {
            return (int)operation;
        }

        public static Result OpenMmcStorage(this StorageService service,
            out ReferenceCountedDisposable<IStorage> storage, MmcPartition partition)
        {
            UnsafeHelpers.SkipParamInit(out storage);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetMmcManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                rc = GetAttribute(out ulong attribute, partition);
                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<IStorageSf> mmcStorage = null;
                ReferenceCountedDisposable<IStorage> tempStorage = null;
                try
                {
                    rc = deviceManager.Target.OpenStorage(out mmcStorage, attribute);
                    if (rc.IsFailure()) return rc;

                    tempStorage = StorageServiceObjectAdapter.CreateShared(ref mmcStorage);

                    if (IsSpeedEmulationNeeded(partition))
                    {
                        tempStorage = SpeedEmulationStorage.CreateShared(ref tempStorage);
                        if (tempStorage is null)
                            return ResultFs.AllocationMemoryFailedCreateShared.Log();
                    }

                    storage = Shared.Move(ref tempStorage);
                    return Result.Success;
                }
                finally
                {
                    mmcStorage?.Dispose();
                    tempStorage?.Dispose();
                }
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        public static Result GetMmcSpeedMode(this StorageService service, out MmcSpeedMode speedMode)
        {
            UnsafeHelpers.SkipParamInit(out speedMode);

            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcOperator(out mmcOperator, MmcPartition.UserData);
                if (rc.IsFailure()) return rc;

                Unsafe.SkipInit(out SpeedMode sdmmcSpeedMode);
                OutBuffer outBuffer = OutBuffer.FromStruct(ref sdmmcSpeedMode);
                int operationId = MakeOperationId(MmcOperationIdValue.GetSpeedMode);

                rc = mmcOperator.Target.OperateOut(out _, outBuffer, operationId);
                if (rc.IsFailure()) return rc;

                speedMode = sdmmcSpeedMode switch
                {
                    SpeedMode.MmcIdentification => MmcSpeedMode.Identification,
                    SpeedMode.MmcLegacySpeed => MmcSpeedMode.LegacySpeed,
                    SpeedMode.MmcHighSpeed => MmcSpeedMode.HighSpeed,
                    SpeedMode.MmcHs200 => MmcSpeedMode.Hs200,
                    SpeedMode.MmcHs400 => MmcSpeedMode.Hs400,
                    _ => MmcSpeedMode.Unknown
                };

                return Result.Success;
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result GetMmcCid(this StorageService service, Span<byte> cidBuffer)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcOperator(out mmcOperator, MmcPartition.UserData);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcOperationIdValue.GetCid);
                var outBuffer = new OutBuffer(cidBuffer);

                return mmcOperator.Target.OperateOut(out _, outBuffer, operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result EraseMmc(this StorageService service, MmcPartition partition)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcOperator(out mmcOperator, partition);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcOperationIdValue.Erase);
                return mmcOperator.Target.Operate(operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result GetMmcPartitionSize(this StorageService service, out long size, MmcPartition partition)
        {
            UnsafeHelpers.SkipParamInit(out size);

            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcOperator(out mmcOperator, partition);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcOperationIdValue.GetPartitionSize);
                OutBuffer outBuffer = OutBuffer.FromStruct(ref size);

                return mmcOperator.Target.OperateOut(out _, outBuffer, operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result GetMmcPatrolCount(this StorageService service, out uint count)
        {
            UnsafeHelpers.SkipParamInit(out count);

            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcManagerOperator(out mmcOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcManagerOperationIdValue.GetPatrolCount);
                OutBuffer outBuffer = OutBuffer.FromStruct(ref count);

                return mmcOperator.Target.OperateOut(out _, outBuffer, operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result GetAndClearMmcErrorInfo(this StorageService service, out StorageErrorInfo errorInfo,
            out long logSize, Span<byte> logBuffer)
        {
            UnsafeHelpers.SkipParamInit(out errorInfo, out logSize);

            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcManagerOperator(out mmcOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcManagerOperationIdValue.GetAndClearErrorInfo);
                var logOutBuffer = new OutBuffer(logBuffer);
                OutBuffer errorInfoOutBuffer = OutBuffer.FromStruct(ref errorInfo);

                return mmcOperator.Target.OperateOut2(out _, errorInfoOutBuffer, out logSize, logOutBuffer,
                    operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result GetMmcExtendedCsd(this StorageService service, Span<byte> buffer)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcOperator(out mmcOperator, MmcPartition.UserData);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcOperationIdValue.GetExtendedCsd);
                var outBuffer = new OutBuffer(buffer);

                return mmcOperator.Target.OperateOut(out _, outBuffer, operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result SuspendMmcPatrol(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcManagerOperator(out mmcOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcManagerOperationIdValue.SuspendPatrol);

                return mmcOperator.Target.Operate(operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result ResumeMmcPatrol(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcManagerOperator(out mmcOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcManagerOperationIdValue.ResumePatrol);

                return mmcOperator.Target.Operate(operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result GetAndClearPatrolReadAllocateBufferCount(this StorageService service,
            out long successCount, out long failureCount)
        {
            UnsafeHelpers.SkipParamInit(out successCount, out failureCount);

            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcManagerOperator(out mmcOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcManagerOperationIdValue.GetAndClearPatrolReadAllocateBufferCount);
                OutBuffer successCountBuffer = OutBuffer.FromStruct(ref successCount);
                OutBuffer failureCountBuffer = OutBuffer.FromStruct(ref failureCount);

                return mmcOperator.Target.OperateOut2(out _, successCountBuffer, out _, failureCountBuffer,
                    operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result SuspendSdmmcControl(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcManagerOperator(out mmcOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcManagerOperationIdValue.SuspendControl);

                return mmcOperator.Target.Operate(operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }

        public static Result ResumeMmcControl(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> mmcOperator = null;
            try
            {
                Result rc = service.GetMmcManagerOperator(out mmcOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(MmcManagerOperationIdValue.ResumeControl);

                return mmcOperator.Target.Operate(operationId);
            }
            finally
            {
                mmcOperator?.Dispose();
            }
        }
    }
}
