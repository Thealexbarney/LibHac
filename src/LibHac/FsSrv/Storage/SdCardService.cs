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

namespace LibHac.FsSrv.Storage;

internal static class SdCardService
{
    private static int MakeOperationId(SdCardManagerOperationIdValue operation) => (int)operation;
    private static int MakeOperationId(SdCardOperationIdValue operation) => (int)operation;

    private static Result GetSdCardManager(this StorageService service,
        ref SharedRef<IStorageDeviceManager> outManager)
    {
        return service.CreateStorageDeviceManager(ref outManager, StorageDevicePortId.SdCard);
    }

    private static Result GetSdCardManagerOperator(this StorageService service,
        ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        return storageDeviceManager.Get.OpenOperator(ref outDeviceOperator);
    }

    private static Result GetSdCardOperator(this StorageService service,
        ref SharedRef<IStorageDeviceOperator> outSdCardOperator)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        using var storageDevice = new SharedRef<IStorageDevice>();
        rc = storageDeviceManager.Get.OpenDevice(ref storageDevice.Ref(), 0);
        if (rc.IsFailure()) return rc;

        return storageDevice.Get.OpenOperator(ref outSdCardOperator);
    }

    public static Result OpenSdStorage(this StorageService service, ref SharedRef<IStorage> outStorage)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        using var sdCardStorage = new SharedRef<IStorageSf>();
        rc = storageDeviceManager.Get.OpenStorage(ref sdCardStorage.Ref(), 0);
        if (rc.IsFailure()) return rc;

        using var storage = new SharedRef<IStorage>(new StorageServiceObjectAdapter(ref sdCardStorage.Ref()));

        SdCardEventSimulator eventSimulator = service.FsSrv.Impl.GetSdCardEventSimulator();
        using var deviceEventSimulationStorage =
            new SharedRef<IStorage>(new DeviceEventSimulationStorage(ref storage.Ref(), eventSimulator));

        using var emulationStorage = new SharedRef<IStorage>(
            new SpeedEmulationStorage(ref deviceEventSimulationStorage.Ref(), service.FsSrv));

        outStorage.SetByMove(ref emulationStorage.Ref());
        return Result.Success;
    }

    public static Result GetCurrentSdCardHandle(this StorageService service, out StorageDeviceHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out handle);

        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        using var sdCardStorageDevice = new SharedRef<IStorageDevice>();
        rc = storageDeviceManager.Get.OpenDevice(ref sdCardStorageDevice.Ref(), 0);
        if (rc.IsFailure()) return rc;

        rc = sdCardStorageDevice.Get.GetHandle(out uint handleValue);
        if (rc.IsFailure()) return rc;

        handle = new StorageDeviceHandle(handleValue, StorageDevicePortId.SdCard);
        return Result.Success;
    }

    public static Result IsSdCardHandleValid(this StorageService service, out bool isValid,
        in StorageDeviceHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out isValid);

        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        // Note: I don't know why the official code doesn't check the handle port
        return storageDeviceManager.Get.IsHandleValid(out isValid, handle.Value);
    }

    public static Result InvalidateSdCard(this StorageService service)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        return storageDeviceManager.Get.Invalidate();
    }

    public static Result IsSdCardInserted(this StorageService service, out bool isInserted)
    {
        UnsafeHelpers.SkipParamInit(out isInserted);

        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc.Miss();

        rc = storageDeviceManager.Get.IsInserted(out bool actualState);
        if (rc.IsFailure()) return rc.Miss();

        isInserted = service.FsSrv.Impl.GetSdCardEventSimulator().FilterDetectionState(actualState);
        return Result.Success;
    }

    public static Result GetSdCardSpeedMode(this StorageService service, out SdCardSpeedMode speedMode)
    {
        UnsafeHelpers.SkipParamInit(out speedMode);

        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        Unsafe.SkipInit(out SpeedMode sdmmcSpeedMode);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref sdmmcSpeedMode);
        int operationId = MakeOperationId(SdCardOperationIdValue.GetSpeedMode);

        rc = sdCardOperator.Get.OperateOut(out _, outBuffer, operationId);
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

    public static Result GetSdCardCid(this StorageService service, Span<byte> cidBuffer)
    {
        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(SdCardOperationIdValue.GetCid);
        var outBuffer = new OutBuffer(cidBuffer);

        return sdCardOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result GetSdCardUserAreaNumSectors(this StorageService service, out uint count)
    {
        UnsafeHelpers.SkipParamInit(out count);

        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(SdCardOperationIdValue.GetUserAreaNumSectors);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref count);

        return sdCardOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result GetSdCardUserAreaSize(this StorageService service, out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(SdCardOperationIdValue.GetUserAreaSize);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref size);

        return sdCardOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result GetSdCardProtectedAreaNumSectors(this StorageService service, out uint count)
    {
        UnsafeHelpers.SkipParamInit(out count);

        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(SdCardOperationIdValue.GetProtectedAreaNumSectors);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref count);

        return sdCardOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result GetSdCardProtectedAreaSize(this StorageService service, out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(SdCardOperationIdValue.GetProtectedAreaSize);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref size);

        return sdCardOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result GetAndClearSdCardErrorInfo(this StorageService service, out StorageErrorInfo errorInfo,
        out long logSize, Span<byte> logBuffer)
    {
        UnsafeHelpers.SkipParamInit(out errorInfo, out logSize);

        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardManagerOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        OutBuffer errorInfoOutBuffer = OutBuffer.FromStruct(ref errorInfo);
        var logOutBuffer = new OutBuffer(logBuffer);
        int operationId = MakeOperationId(SdCardManagerOperationIdValue.GetAndClearErrorInfo);

        return sdCardOperator.Get.OperateOut2(out _, errorInfoOutBuffer, out logSize, logOutBuffer, operationId);
    }

    public static Result OpenSdCardDetectionEvent(this StorageService service,
        ref SharedRef<IEventNotifier> outEventNotifier)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetSdCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return storageDeviceManager.Get.OpenDetectionEvent(ref outEventNotifier);
    }

    public static Result SimulateSdCardDetectionEventSignaled(this StorageService service)
    {
        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardManagerOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        return sdCardOperator.Get.Operate(
            MakeOperationId(SdCardManagerOperationIdValue.SimulateDetectionEventSignaled));
    }

    public static Result SuspendSdCardControl(this StorageService service)
    {
        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardManagerOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        return sdCardOperator.Get.Operate(MakeOperationId(SdCardManagerOperationIdValue.SuspendControl));
    }

    public static Result ResumeSdCardControl(this StorageService service)
    {
        using var sdCardOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetSdCardManagerOperator(ref sdCardOperator.Ref());
        if (rc.IsFailure()) return rc;

        return sdCardOperator.Get.Operate(MakeOperationId(SdCardManagerOperationIdValue.ResumeControl));
    }
}
