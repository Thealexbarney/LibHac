﻿using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Impl;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Gc;
using LibHac.GcSrv;
using LibHac.Os;
using LibHac.Sf;
using static LibHac.Gc.Values;
using IStorage = LibHac.Fs.IStorage;

namespace LibHac.FsSrv.Storage;

internal struct GameCardServiceGlobals : IDisposable
{
    public SdkMutexType StorageDeviceMutex;
    public SharedRef<IStorageDevice> CachedStorageDevice;

    public void Initialize()
    {
        StorageDeviceMutex = new SdkMutexType();
    }

    public void Dispose()
    {
        CachedStorageDevice.Destroy();
    }
}

/// <summary>
/// Contains functions for interacting with the game card storage device.
/// </summary>
/// <remarks>Based on FS 14.1.0 (nnSdk 14.3.0)</remarks>
internal static class GameCardService
{
    private static ulong MakeAttributeId(OpenGameCardAttribute attribute) => (ulong)attribute;
    private static int MakeOperationId(GameCardManagerOperationIdValue operation) => (int)operation;
    private static int MakeOperationId(GameCardOperationIdValue operation) => (int)operation;

    private static Result GetGameCardManager(this StorageService service,
        ref SharedRef<IStorageDeviceManager> outManager)
    {
        return service.CreateStorageDeviceManager(ref outManager, StorageDevicePortId.GameCard);
    }

    private static Result OpenAndCacheGameCardDevice(this StorageService service,
        ref SharedRef<IStorageDevice> outStorageDevice, OpenGameCardAttribute attribute)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetGameCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc.Miss();

        ref GameCardServiceGlobals g = ref service.Globals.GameCardService;

        using var gameCardStorageDevice = new SharedRef<IStorageDevice>();
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref g.StorageDeviceMutex);

        rc = storageDeviceManager.Get.OpenDevice(ref gameCardStorageDevice.Ref(), MakeAttributeId(attribute));
        if (rc.IsFailure()) return rc.Miss();

        g.CachedStorageDevice.SetByCopy(in gameCardStorageDevice);
        outStorageDevice.SetByCopy(in gameCardStorageDevice);

        return Result.Success;
    }

    private static Result GetGameCardManagerOperator(this StorageService service,
        ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetGameCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return storageDeviceManager.Get.OpenOperator(ref outDeviceOperator);
    }

    private static Result GetGameCardOperator(this StorageService service,
        ref SharedRef<IStorageDeviceOperator> outDeviceOperator, OpenGameCardAttribute attribute)
    {
        using var storageDevice = new SharedRef<IStorageDevice>();
        Result rc = service.OpenAndCacheGameCardDevice(ref storageDevice.Ref(), attribute);
        if (rc.IsFailure()) return rc.Miss();

        return storageDevice.Get.OpenOperator(ref outDeviceOperator.Ref());
    }

    private static Result GetGameCardOperator(this StorageService service,
        ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        ref GameCardServiceGlobals g = ref service.Globals.GameCardService;

        using (ScopedLock.Lock(ref g.StorageDeviceMutex))
        {
            if (g.CachedStorageDevice.HasValue)
            {
                return g.CachedStorageDevice.Get.OpenOperator(ref outDeviceOperator);
            }
        }

        return service.GetGameCardOperator(ref outDeviceOperator, OpenGameCardAttribute.ReadOnly);
    }

    public static Result OpenGameCardStorage(this StorageService service, ref SharedRef<IStorage> outStorage,
        OpenGameCardAttribute attribute, GameCardHandle handle)
    {
        using var gameCardStorageDevice = new SharedRef<IStorageDevice>();

        Result rc = service.OpenAndCacheGameCardDevice(ref gameCardStorageDevice.Ref(), attribute);
        if (rc.IsFailure()) return rc.Miss();

        // Verify that the game card handle hasn't changed.
        rc = service.GetCurrentGameCardHandle(out StorageDeviceHandle newHandle);
        if (rc.IsFailure()) return rc.Miss();

        if (newHandle.Value != handle)
        {
            switch (attribute)
            {
                case OpenGameCardAttribute.ReadOnly:
                    return ResultFs.GameCardFsCheckHandleInCreateReadOnlyFailure.Log();
                case OpenGameCardAttribute.SecureReadOnly:
                    return ResultFs.GameCardFsCheckHandleInCreateSecureReadOnlyFailure.Log();
                case OpenGameCardAttribute.WriteOnly:
                    break;
                default:
                    return ResultFs.GameCardFsFailure.Log();
            }
        }

        // Open the storage and add IPC and event simulation wrappers.
        using var storage = new SharedRef<IStorage>(new StorageServiceObjectAdapter(ref gameCardStorageDevice.Ref()));

        using var deviceEventSimulationStorage = new SharedRef<DeviceEventSimulationStorage>(
            new DeviceEventSimulationStorage(ref storage.Ref(), service.FsSrv.Impl.GetGameCardEventSimulator()));

        outStorage.SetByMove(ref deviceEventSimulationStorage.Ref());

        return Result.Success;
    }

    public static Result GetCurrentGameCardHandle(this StorageService service, out StorageDeviceHandle outHandle)
    {
        UnsafeHelpers.SkipParamInit(out outHandle);

        ref GameCardServiceGlobals g = ref service.Globals.GameCardService;

        using (ScopedLock.Lock(ref g.StorageDeviceMutex))
        {
            if (g.CachedStorageDevice.HasValue)
            {
                Result rc = g.CachedStorageDevice.Get.GetHandle(out GameCardHandle handle);
                if (rc.IsFailure()) return rc.Miss();

                outHandle = new StorageDeviceHandle(handle, StorageDevicePortId.GameCard);
                return Result.Success;
            }
        }

        {
            using var gameCardStorageDevice = new SharedRef<IStorageDevice>();
            Result rc = service.OpenAndCacheGameCardDevice(ref gameCardStorageDevice.Ref(),
                OpenGameCardAttribute.ReadOnly);
            if (rc.IsFailure()) return rc.Miss();

            rc = gameCardStorageDevice.Get.GetHandle(out GameCardHandle handleValue);
            if (rc.IsFailure()) return rc.Miss();

            outHandle = new StorageDeviceHandle(handleValue, StorageDevicePortId.GameCard);
            return Result.Success;
        }
    }

    public static Result IsGameCardHandleValid(this StorageService service, out bool isValid,
        in StorageDeviceHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out isValid);

        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetGameCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return storageDeviceManager.Get.IsHandleValid(out isValid, handle.Value);
    }

    public static Result IsGameCardInserted(this StorageService service, out bool outIsInserted)
    {
        UnsafeHelpers.SkipParamInit(out outIsInserted);

        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetGameCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc.Miss();

        // Get the actual state of the game card.
        rc = storageDeviceManager.Get.IsInserted(out bool isInserted);
        if (rc.IsFailure()) return rc.Miss();

        // Get the simulated state of the game card based on the actual state.
        outIsInserted = service.FsSrv.Impl.GetGameCardEventSimulator().FilterDetectionState(isInserted);
        return Result.Success;
    }

    public static Result EraseGameCard(this StorageService service, uint gameCardSize, ulong romAreaStartPageAddress)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        InBuffer inBuffer = InBuffer.FromStruct(in romAreaStartPageAddress);
        int operationId = MakeOperationId(GameCardOperationIdValue.EraseGameCard);

        return gcOperator.Get.OperateIn(inBuffer, offset: 0, gameCardSize, operationId);
    }

    public static Result GetInitializationResult(this StorageService service)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        int operationId = MakeOperationId(GameCardManagerOperationIdValue.GetInitializationResult);

        return gcOperator.Get.Operate(operationId);
    }

    public static Result GetGameCardStatus(this StorageService service, out GameCardStatus outGameCardStatus,
        GameCardHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out outGameCardStatus);

        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        // Verify that the game card handle hasn't changed.
        var deviceHandle = new StorageDeviceHandle(handle, StorageDevicePortId.GameCard);
        rc = service.IsGameCardHandleValid(out bool isValidHandle, in deviceHandle);
        if (rc.IsFailure()) return rc.Miss();

        if (!isValidHandle)
            return ResultFs.GameCardFsCheckHandleInGetStatusFailure.Log();

        // Get the GameCardStatus.
        OutBuffer outCardStatusBuffer = OutBuffer.FromStruct(ref outGameCardStatus);
        int operationId = MakeOperationId(GameCardOperationIdValue.GetGameCardStatus);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, outCardStatusBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(Unsafe.SizeOf<GameCardStatus>(), bytesWritten);

        return Result.Success;
    }

    public static Result FinalizeGameCardLibrary(this StorageService service)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        int operationId = MakeOperationId(GameCardManagerOperationIdValue.Finalize);

        return gcOperator.Get.Operate(operationId);
    }

    public static Result GetGameCardDeviceCertificate(this StorageService service, Span<byte> outBuffer,
        GameCardHandle handle)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        // Verify that the game card handle hasn't changed.
        var deviceHandle = new StorageDeviceHandle(handle, StorageDevicePortId.GameCard);
        rc = service.IsGameCardHandleValid(out bool isValidHandle, in deviceHandle);
        if (rc.IsFailure()) return rc.Miss();

        if (!isValidHandle)
            return ResultFs.GameCardFsCheckHandleInGetDeviceCertFailure.Log();

        // Get the device certificate.
        var outCertBuffer = new OutBuffer(outBuffer);
        int operationId = MakeOperationId(GameCardOperationIdValue.GetGameCardDeviceCertificate);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, outCertBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(GcDeviceCertificateSize, bytesWritten);

        return Result.Success;
    }

    public static Result ChallengeCardExistence(this StorageService service, Span<byte> outResponseBuffer,
        ReadOnlySpan<byte> challengeSeed, ReadOnlySpan<byte> challengeValue, GameCardHandle handle)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        // Verify that the game card handle hasn't changed.
        var deviceHandle = new StorageDeviceHandle(handle, StorageDevicePortId.GameCard);
        rc = service.IsGameCardHandleValid(out bool isValidHandle, in deviceHandle);
        if (rc.IsFailure()) return rc.Miss();

        if (!isValidHandle)
            return ResultFs.GameCardFsCheckHandleInChallengeCardExistence.Log();

        // Get the challenge response.
        var valueBuffer = new InBuffer(challengeValue);
        var seedBuffer = new InBuffer(challengeSeed);
        var responseBuffer = new OutBuffer(outResponseBuffer);
        int operationId = MakeOperationId(GameCardOperationIdValue.ChallengeCardExistence);

        rc = gcOperator.Get.OperateIn2Out(out long bytesWritten, responseBuffer, valueBuffer, seedBuffer, offset: 0,
            size: 0, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(GcCardExistenceResponseDataSize, bytesWritten);

        return Result.Success;
    }

    public static Result GetGameCardHandle(this StorageService service, out GameCardHandle outHandle)
    {
        UnsafeHelpers.SkipParamInit(out outHandle);

        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        // Get the current handle.
        OutBuffer handleOutBuffer = OutBuffer.FromStruct(ref outHandle);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.GetHandle);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, handleOutBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(Unsafe.SizeOf<GameCardHandle>(), bytesWritten);

        // Clear the cached storage device if it has an old handle.
        ref GameCardServiceGlobals g = ref service.Globals.GameCardService;
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref g.StorageDeviceMutex);

        if (g.CachedStorageDevice.HasValue)
        {
            g.CachedStorageDevice.Get.GetHandle(out GameCardHandle handleValue);
            if (rc.IsFailure()) return rc.Miss();

            var currentHandle = new StorageDeviceHandle(handleValue, StorageDevicePortId.GameCard);
            rc = service.IsGameCardHandleValid(out bool isHandleValid, in currentHandle);
            if (rc.IsFailure()) return rc.Miss();

            if (!isHandleValid)
                g.CachedStorageDevice.Reset();
        }

        return Result.Success;
    }

    public static Result GetGameCardAsicInfo(this StorageService service, out RmaInformation rmaInfo,
        ReadOnlySpan<byte> firmwareBuffer)
    {
        UnsafeHelpers.SkipParamInit(out rmaInfo);

        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        var inFirmwareBuffer = new InBuffer(firmwareBuffer);
        OutBuffer outRmaInfoBuffer = OutBuffer.FromStruct(ref rmaInfo);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.GetGameCardAsicInfo);

        rc = gcOperator.Get.OperateInOut(out long bytesWritten, outRmaInfoBuffer, inFirmwareBuffer, offset: 0,
            size: firmwareBuffer.Length, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(Unsafe.SizeOf<RmaInformation>(), bytesWritten);

        return Result.Success;
    }

    public static Result GetGameCardIdSet(this StorageService service, out GameCardIdSet outIdSet)
    {
        UnsafeHelpers.SkipParamInit(out outIdSet);

        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        OutBuffer outIdSetBuffer = OutBuffer.FromStruct(ref outIdSet);
        int operationId = MakeOperationId(GameCardOperationIdValue.GetGameCardIdSet);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, outIdSetBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(Unsafe.SizeOf<GameCardIdSet>(), bytesWritten);

        return Result.Success;
    }

    public static Result WriteToGameCardDirectly(this StorageService service, long offset, Span<byte> buffer)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        var outBuffer = new OutBuffer(buffer);
        var inUnusedBuffer = new InBuffer();
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.WriteToGameCardDirectly);

        // Missing: Register device buffer

        rc = gcOperator.Get.OperateInOut(out _, outBuffer, inUnusedBuffer, offset, buffer.Length, operationId);

        // Missing: Unregister device buffer

        return rc;
    }

    public static Result SetVerifyWriteEnableFlag(this StorageService service, bool isEnabled)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        InBuffer inIsEnabledBuffer = InBuffer.FromStruct(in isEnabled);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.SetVerifyEnableFlag);

        return gcOperator.Get.OperateIn(inIsEnabledBuffer, offset: 0, size: 0, operationId);
    }

    public static Result GetGameCardImageHash(this StorageService service, Span<byte> outBuffer, GameCardHandle handle)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        // Verify that the game card handle hasn't changed.
        var deviceHandle = new StorageDeviceHandle(handle, StorageDevicePortId.GameCard);
        rc = service.IsGameCardHandleValid(out bool isValidHandle, in deviceHandle);
        if (rc.IsFailure()) return rc.Miss();

        if (!isValidHandle)
            return ResultFs.GameCardFsCheckHandleInGetCardImageHashFailure.Log();

        // Get the card image hash.
        var outImageHashBuffer = new OutBuffer(outBuffer);
        int operationId = MakeOperationId(GameCardOperationIdValue.GetGameCardImageHash);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, outImageHashBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(GcCardImageHashSize, bytesWritten);

        return Result.Success;
    }

    public static Result GetGameCardDeviceIdForProdCard(this StorageService service, Span<byte> outBuffer,
        ReadOnlySpan<byte> devHeaderBuffer)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        var inDevHeaderBuffer = new InBuffer(devHeaderBuffer);
        var outDeviceIdBuffer = new OutBuffer(outBuffer);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.GetGameCardDeviceIdForProdCard);

        rc = gcOperator.Get.OperateInOut(out long bytesWritten, outDeviceIdBuffer, inDevHeaderBuffer, offset: 0,
            size: 0, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(GcPageSize, bytesWritten);

        return Result.Success;
    }

    public static Result EraseAndWriteParamDirectly(this StorageService service, ReadOnlySpan<byte> devParamBuffer)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        var inDevParamBuffer = new InBuffer(devParamBuffer);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.EraseAndWriteParamDirectly);

        return gcOperator.Get.OperateIn(inDevParamBuffer, offset: 0, size: devParamBuffer.Length, operationId);
    }

    public static Result ReadParamDirectly(this StorageService service, Span<byte> outDevParamBuffer)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        int operationId = MakeOperationId(GameCardManagerOperationIdValue.ReadParamDirectly);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, new OutBuffer(outDevParamBuffer), operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(GcPageSize, bytesWritten);

        return Result.Success;
    }

    public static Result ForceEraseGameCard(this StorageService service)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        int operationId = MakeOperationId(GameCardManagerOperationIdValue.ForceErase);

        return gcOperator.Get.Operate(operationId);
    }

    public static Result GetGameCardErrorInfo(this StorageService service, out GameCardErrorInfo errorInfo)
    {
        UnsafeHelpers.SkipParamInit(out errorInfo);

        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        OutBuffer outErrorInfoBuffer = OutBuffer.FromStruct(ref errorInfo);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.GetGameCardErrorInfo);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, outErrorInfoBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(Unsafe.SizeOf<GameCardErrorInfo>(), bytesWritten);

        return Result.Success;
    }

    public static Result GetGameCardErrorReportInfo(this StorageService service,
        out GameCardErrorReportInfo errorReportInfo)
    {
        UnsafeHelpers.SkipParamInit(out errorReportInfo);

        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        OutBuffer outErrorReportInfoBuffer = OutBuffer.FromStruct(ref errorReportInfo);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.GetGameCardErrorReportInfo);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, outErrorReportInfoBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(Unsafe.SizeOf<GameCardErrorReportInfo>(), bytesWritten);

        return Result.Success;
    }

    public static Result GetGameCardDeviceId(this StorageService service, Span<byte> outBuffer)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        var outDeviceIdBuffer = new OutBuffer(outBuffer);
        int operationId = MakeOperationId(GameCardOperationIdValue.GetGameCardDeviceId);

        rc = gcOperator.Get.OperateOut(out long bytesWritten, outDeviceIdBuffer, operationId);
        if (rc.IsFailure()) return rc.Miss();

        Assert.SdkEqual(GcCardDeviceIdSize, bytesWritten);

        return Result.Success;
    }

    public static bool IsGameCardActivationValid(this StorageService service, GameCardHandle handle)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return Result.ConvertResultToReturnType<bool>(rc);

        bool isValid = false;
        InBuffer inHandleBuffer = InBuffer.FromStruct(in handle);
        OutBuffer outIsValidBuffer = OutBuffer.FromStruct(ref isValid);
        int operationId = MakeOperationId(GameCardManagerOperationIdValue.IsGameCardActivationValid);

        rc = gcOperator.Get.OperateInOut(out long bytesWritten, outIsValidBuffer, inHandleBuffer, offset: 0, size: 0,
            operationId);
        if (rc.IsFailure()) return Result.ConvertResultToReturnType<bool>(rc);

        Assert.SdkEqual(Unsafe.SizeOf<bool>(), bytesWritten);

        return isValid;
    }

    public static Result OpenGameCardDetectionEvent(this StorageService service,
        ref SharedRef<IEventNotifier> outEventNotifier)
    {
        using var storageDeviceManager = new SharedRef<IStorageDeviceManager>();
        Result rc = service.GetGameCardManager(ref storageDeviceManager.Ref());
        if (rc.IsFailure()) return rc.Miss();

        return storageDeviceManager.Get.OpenDetectionEvent(ref outEventNotifier);
    }

    public static Result SimulateGameCardDetectionEventSignaled(this StorageService service)
    {
        using var gcOperator = new SharedRef<IStorageDeviceOperator>();
        Result rc = service.GetGameCardManagerOperator(ref gcOperator.Ref());
        if (rc.IsFailure()) return rc.Miss();

        int operationId = MakeOperationId(GameCardManagerOperationIdValue.SimulateDetectionEventSignaled);

        return gcOperator.Get.Operate(operationId);
    }
}