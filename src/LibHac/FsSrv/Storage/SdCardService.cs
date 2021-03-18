using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sdmmc;
using LibHac.SdmmcSrv;
using LibHac.Sf;
using IStorage = LibHac.Fs.IStorage;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.FsSrv.Storage
{
    internal static class SdCardService
    {
        private static Result GetSdCardManager(this StorageService service,
            out ReferenceCountedDisposable<IStorageDeviceManager> manager)
        {
            return service.CreateStorageDeviceManager(out manager, StorageDevicePortId.SdCard);
        }

        private static Result GetSdCardManagerOperator(this StorageService service,
            out ReferenceCountedDisposable<IStorageDeviceOperator> deviceOperator)
        {
            UnsafeHelpers.SkipParamInit(out deviceOperator);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                return deviceManager.Target.OpenOperator(out deviceOperator);
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        private static Result GetSdCardOperator(this StorageService service,
            out ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator)
        {
            UnsafeHelpers.SkipParamInit(out sdCardOperator);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<IStorageDevice> storageDevice = null;
                try
                {
                    rc = deviceManager.Target.OpenDevice(out storageDevice, 0);
                    if (rc.IsFailure()) return rc;

                    return storageDevice.Target.OpenOperator(out sdCardOperator);
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

        private static int MakeOperationId(SdCardManagerOperationIdValue operation)
        {
            return (int)operation;
        }

        private static int MakeOperationId(SdCardOperationIdValue operation)
        {
            return (int)operation;
        }

        public static Result OpenSdStorage(this StorageService service,
            out ReferenceCountedDisposable<IStorage> storage)
        {
            UnsafeHelpers.SkipParamInit(out storage);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<IStorageSf> sdCardStorage = null;
                ReferenceCountedDisposable<IStorage> tempStorage = null;
                try
                {
                    rc = deviceManager.Target.OpenStorage(out sdCardStorage, 0);
                    if (rc.IsFailure()) return rc;

                    tempStorage = StorageServiceObjectAdapter.CreateShared(ref sdCardStorage);

                    SdCardEventSimulator eventSimulator = service.FsSrv.Impl.GetSdCardEventSimulator();
                    tempStorage = DeviceEventSimulationStorage.CreateShared(ref tempStorage, eventSimulator);

                    tempStorage = SpeedEmulationStorage.CreateShared(ref tempStorage);

                    storage = Shared.Move(ref tempStorage);
                    return Result.Success;
                }
                finally
                {
                    sdCardStorage?.Dispose();
                    tempStorage?.Dispose();
                }
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        public static Result GetCurrentSdCardHandle(this StorageService service, out StorageDeviceHandle handle)
        {
            UnsafeHelpers.SkipParamInit(out handle);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                ReferenceCountedDisposable<IStorageDevice> storageDevice = null;
                try
                {
                    rc = deviceManager.Target.OpenDevice(out storageDevice, 0);
                    if (rc.IsFailure()) return rc;

                    rc = storageDevice.Target.GetHandle(out uint handleValue);
                    if (rc.IsFailure()) return rc;

                    handle = new StorageDeviceHandle(handleValue, StorageDevicePortId.SdCard);
                    return Result.Success;
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

        public static Result IsSdCardHandleValid(this StorageService service, out bool isValid,
            in StorageDeviceHandle handle)
        {
            UnsafeHelpers.SkipParamInit(out isValid);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                // Note: I don't know why the official code doesn't check the handle port
                return deviceManager.Target.IsHandleValid(out isValid, handle.Value);
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        public static Result InvalidateSdCard(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                return deviceManager.Target.Invalidate();
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        public static Result IsSdCardInserted(this StorageService service, out bool isInserted)
        {
            UnsafeHelpers.SkipParamInit(out isInserted);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                return deviceManager.Target.Invalidate();
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        public static Result GetSdCardSpeedMode(this StorageService service, out SdCardSpeedMode speedMode)
        {
            UnsafeHelpers.SkipParamInit(out speedMode);

            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                Unsafe.SkipInit(out SpeedMode sdmmcSpeedMode);
                OutBuffer outBuffer = OutBuffer.FromStruct(ref sdmmcSpeedMode);
                int operationId = MakeOperationId(SdCardOperationIdValue.GetSpeedMode);

                rc = sdCardOperator.Target.OperateOut(out _, outBuffer, operationId);
                if (rc.IsFailure()) return rc;

                speedMode = sdmmcSpeedMode switch
                {
                    SpeedMode.SdCardIdentification => SdCardSpeedMode.Identification,
                    SpeedMode.SdCardDefaultSpeed => SdCardSpeedMode.DefaultSpeed,
                    SpeedMode.SdCardHighSpeed => SdCardSpeedMode.HighSpeed,
                    SpeedMode.SdCardSdr12 => SdCardSpeedMode.Sdr12,
                    SpeedMode.SdCardSdr25 => SdCardSpeedMode.Sdr25,
                    SpeedMode.SdCardSdr50 => SdCardSpeedMode.Sdr50,
                    SpeedMode.SdCardSdr104 => SdCardSpeedMode.Sdr104,
                    SpeedMode.SdCardDdr50 => SdCardSpeedMode.Ddr50,
                    _ => SdCardSpeedMode.Unknown
                };

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result GetSdCardCid(this StorageService service, Span<byte> cidBuffer)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                var outBuffer = new OutBuffer(cidBuffer);
                int operationId = MakeOperationId(SdCardOperationIdValue.GetCid);

                rc = sdCardOperator.Target.OperateOut(out _, outBuffer, operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result GetSdCardUserAreaNumSectors(this StorageService service, out uint count)
        {
            UnsafeHelpers.SkipParamInit(out count);

            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                OutBuffer outBuffer = OutBuffer.FromStruct(ref count);
                int operationId = MakeOperationId(SdCardOperationIdValue.GetUserAreaNumSectors);

                rc = sdCardOperator.Target.OperateOut(out _, outBuffer, operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result GetSdCardUserAreaSize(this StorageService service, out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                OutBuffer outBuffer = OutBuffer.FromStruct(ref size);
                int operationId = MakeOperationId(SdCardOperationIdValue.GetUserAreaSize);

                rc = sdCardOperator.Target.OperateOut(out _, outBuffer, operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result GetSdCardProtectedAreaNumSectors(this StorageService service, out uint count)
        {
            UnsafeHelpers.SkipParamInit(out count);

            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                OutBuffer outBuffer = OutBuffer.FromStruct(ref count);
                int operationId = MakeOperationId(SdCardOperationIdValue.GetProtectedAreaNumSectors);

                rc = sdCardOperator.Target.OperateOut(out _, outBuffer, operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result GetSdCardProtectedAreaSize(this StorageService service, out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                OutBuffer outBuffer = OutBuffer.FromStruct(ref size);
                int operationId = MakeOperationId(SdCardOperationIdValue.GetProtectedAreaSize);

                rc = sdCardOperator.Target.OperateOut(out _, outBuffer, operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result GetAndClearSdCardErrorInfo(this StorageService service, out StorageErrorInfo errorInfo,
            out long logSize, Span<byte> logBuffer)
        {
            UnsafeHelpers.SkipParamInit(out errorInfo, out logSize);

            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardManagerOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                OutBuffer errorInfoOutBuffer = OutBuffer.FromStruct(ref errorInfo);
                var logOutBuffer = new OutBuffer(logBuffer);
                int operationId = MakeOperationId(SdCardManagerOperationIdValue.GetAndClearErrorInfo);

                rc = sdCardOperator.Target.OperateOut2(out _, errorInfoOutBuffer, out logSize, logOutBuffer,
                    operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result OpenSdCardDetectionEvent(this StorageService service,
            out ReferenceCountedDisposable<IEventNotifier> eventNotifier)
        {
            UnsafeHelpers.SkipParamInit(out eventNotifier);

            ReferenceCountedDisposable<IStorageDeviceManager> deviceManager = null;
            try
            {
                Result rc = service.GetSdCardManager(out deviceManager);
                if (rc.IsFailure()) return rc;

                return deviceManager.Target.OpenDetectionEvent(out eventNotifier);
            }
            finally
            {
                deviceManager?.Dispose();
            }
        }

        public static Result SimulateSdCardDetectionEventSignaled(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardManagerOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(SdCardManagerOperationIdValue.SimulateDetectionEventSignaled);

                rc = sdCardOperator.Target.Operate(operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result SuspendSdCardControl(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardManagerOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(SdCardManagerOperationIdValue.SuspendControl);

                rc = sdCardOperator.Target.Operate(operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }

        public static Result ResumeSdCardControl(this StorageService service)
        {
            ReferenceCountedDisposable<IStorageDeviceOperator> sdCardOperator = null;
            try
            {
                Result rc = service.GetSdCardManagerOperator(out sdCardOperator);
                if (rc.IsFailure()) return rc;

                int operationId = MakeOperationId(SdCardManagerOperationIdValue.ResumeControl);

                rc = sdCardOperator.Target.Operate(operationId);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }
            finally
            {
                sdCardOperator?.Dispose();
            }
        }
    }
}
