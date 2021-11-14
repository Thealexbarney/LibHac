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

namespace LibHac.FsSrv.Storage;

internal static class MmcService
{
    private static int MakeOperationId(MmcManagerOperationIdValue operation) => (int)operation;
    private static int MakeOperationId(MmcOperationIdValue operation) => (int)operation;

    private static Result GetMmcManager(this StorageService service,
        ref SharedRef<IStorageDeviceManager> outManager)
    {
        return service.CreateStorageDeviceManager(ref outManager, StorageDevicePortId.Mmc);
    }

    private static Result GetMmcManagerOperator(this StorageService service,
        ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetMmcManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        return storageDeviceManager.Get.OpenOperator(ref outDeviceOperator);
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
        ref SharedRef<IStorageDeviceOperator> outMmcOperator, MmcPartition partition)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetMmcManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        rc = GetAttribute(out ulong attribute, partition);
        if (rc.IsFailure()) return rc;

        using var storageDevice = new SharedRef<IStorageDevice>();
        rc = storageDeviceManager.Get.OpenDevice(ref storageDevice.Ref(), attribute);
        if (rc.IsFailure()) return rc;

        return storageDevice.Get.OpenOperator(ref outMmcOperator);
    }

    public static Result OpenMmcStorage(this StorageService service, ref SharedRef<IStorage> outStorage,
        MmcPartition partition)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetMmcManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc;

        rc = GetAttribute(out ulong attribute, partition);
        if (rc.IsFailure()) return rc;

        using var mmcStorage = new SharedRef<IStorageSf>();
        rc = storageDeviceManager.Get.OpenStorage(ref mmcStorage.Ref(), attribute);
        if (rc.IsFailure()) return rc;

        using var storage = new SharedRef<IStorage>(new StorageServiceObjectAdapter(ref mmcStorage.Ref()));

        if (IsSpeedEmulationNeeded(partition))
        {
            using var emulationStorage =
                new SharedRef<IStorage>(new SpeedEmulationStorage(ref storage.Ref(), service.FsSrv));

            outStorage.SetByMove(ref emulationStorage.Ref());
            return Result.Success;
        }

        outStorage.SetByMove(ref storage.Ref());
        return Result.Success;
    }

    public static Result GetMmcSpeedMode(this StorageService service, out MmcSpeedMode speedMode)
    {
        UnsafeHelpers.SkipParamInit(out speedMode);

        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcOperator(ref mmcOperator.Ref(), MmcPartition.UserData);
        if (rc.IsFailure()) return rc;

        Unsafe.SkipInit(out SpeedMode sdmmcSpeedMode);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref sdmmcSpeedMode);
        int operationId = MakeOperationId(MmcOperationIdValue.GetSpeedMode);

        rc = mmcOperator.Get.OperateOut(out _, outBuffer, operationId);
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

    public static Result GetMmcCid(this StorageService service, Span<byte> cidBuffer)
    {
        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcOperator(ref mmcOperator.Ref(), MmcPartition.UserData);
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(MmcOperationIdValue.GetCid);
        var outBuffer = new OutBuffer(cidBuffer);

        return mmcOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result EraseMmc(this StorageService service, MmcPartition partition)
    {
        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcOperator(ref mmcOperator.Ref(), MmcPartition.UserData);
        if (rc.IsFailure()) return rc;

        return mmcOperator.Get.Operate(MakeOperationId(MmcOperationIdValue.Erase));
    }

    public static Result GetMmcPartitionSize(this StorageService service, out long size, MmcPartition partition)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcOperator(ref mmcOperator.Ref(), MmcPartition.UserData);
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(MmcOperationIdValue.GetPartitionSize);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref size);

        return mmcOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result GetMmcPatrolCount(this StorageService service, out uint count)
    {
        UnsafeHelpers.SkipParamInit(out count);

        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcManagerOperator(ref mmcOperator.Ref());
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(MmcManagerOperationIdValue.GetPatrolCount);
        OutBuffer outBuffer = OutBuffer.FromStruct(ref count);

        return mmcOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result GetAndClearMmcErrorInfo(this StorageService service, out StorageErrorInfo errorInfo,
        out long logSize, Span<byte> logBuffer)
    {
        UnsafeHelpers.SkipParamInit(out errorInfo, out logSize);

        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcManagerOperator(ref mmcOperator.Ref());
        if (rc.IsFailure()) return rc;

        OutBuffer errorInfoOutBuffer = OutBuffer.FromStruct(ref errorInfo);
        var logOutBuffer = new OutBuffer(logBuffer);
        int operationId = MakeOperationId(MmcManagerOperationIdValue.GetAndClearErrorInfo);

        return mmcOperator.Get.OperateOut2(out _, errorInfoOutBuffer, out logSize, logOutBuffer, operationId);
    }

    public static Result GetMmcExtendedCsd(this StorageService service, Span<byte> buffer)
    {
        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcOperator(ref mmcOperator.Ref(), MmcPartition.UserData);
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(MmcOperationIdValue.GetExtendedCsd);
        var outBuffer = new OutBuffer(buffer);

        return mmcOperator.Get.OperateOut(out _, outBuffer, operationId);
    }

    public static Result SuspendMmcPatrol(this StorageService service)
    {
        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcManagerOperator(ref mmcOperator.Ref());
        if (rc.IsFailure()) return rc;

        return mmcOperator.Get.Operate(MakeOperationId(MmcManagerOperationIdValue.SuspendPatrol));
    }

    public static Result ResumeMmcPatrol(this StorageService service)
    {
        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcManagerOperator(ref mmcOperator.Ref());
        if (rc.IsFailure()) return rc;

        return mmcOperator.Get.Operate(MakeOperationId(MmcManagerOperationIdValue.ResumePatrol));
    }

    public static Result GetAndClearPatrolReadAllocateBufferCount(this StorageService service,
        out long successCount, out long failureCount)
    {
        UnsafeHelpers.SkipParamInit(out successCount, out failureCount);

        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcManagerOperator(ref mmcOperator.Ref());
        if (rc.IsFailure()) return rc;

        int operationId = MakeOperationId(MmcManagerOperationIdValue.GetAndClearPatrolReadAllocateBufferCount);
        OutBuffer successCountBuffer = OutBuffer.FromStruct(ref successCount);
        OutBuffer failureCountBuffer = OutBuffer.FromStruct(ref failureCount);

        return mmcOperator.Get.OperateOut2(out _, successCountBuffer, out _, failureCountBuffer, operationId);
    }

    public static Result SuspendSdmmcControl(this StorageService service)
    {
        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcManagerOperator(ref mmcOperator.Ref());
        if (rc.IsFailure()) return rc;

        return mmcOperator.Get.Operate(MakeOperationId(MmcManagerOperationIdValue.SuspendControl));
    }

    public static Result ResumeMmcControl(this StorageService service)
    {
        using var mmcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetMmcManagerOperator(ref mmcOperator.Ref());
        if (rc.IsFailure()) return rc;

        return mmcOperator.Get.Operate(MakeOperationId(MmcManagerOperationIdValue.ResumeControl));
    }
}
