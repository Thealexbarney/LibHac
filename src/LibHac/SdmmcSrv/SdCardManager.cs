using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;
using LibHac.Util;
using IStorage = LibHac.Fs.IStorage;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Manages the state of the SD card and allows reading and writing the SD card storage.
/// </summary>
/// <remarks><para>This class implements the <see cref="IStorageDeviceOperator"/> interface, and all available
/// operations are listed in <see cref="SdCardManagerOperationIdValue"/>.</para>
/// <para>Based on nnSdk 15.3.0 (FS 15.0.0)</para></remarks>
public class SdCardManager : IStorageDeviceManager, IStorageDeviceOperator, ISdmmcDeviceManager
{
    private const SdmmcHandle InvalidHandle = 0;
    private const int OpenCountFinalized = -1;

    private readonly Port _port;
    private bool _isInitialized;

    /// <summary>
    /// Tracks how many storage devices are open. A value of -1 indicates that the SD card device
    /// has been shut down and won't be opened again.
    /// </summary>
    private int _openCount;
    private SdkMutexType _mutex;
    private readonly SdmmcStorage _sdStorage;
    private SdmmcHandle _handle;
    private Optional<SdCardDetectionEventManager> _detectionEventManager;

    // LibHac additions
    private WeakRef<SdCardManager> _selfReference;
    private readonly SdmmcApi _sdmmc;

    private SdCardManager(SdmmcApi sdmmc)
    {
        // Missing: An optional parameter with the device address space info is passed in and stored in the SdCardManager.

        _port = Port.SdCard0;
        _mutex = new SdkMutexType();
        _sdStorage = new SdmmcStorage(_port, sdmmc);
        _sdmmc = sdmmc;
    }

    public static SharedRef<SdCardManager> CreateShared(SdmmcApi sdmmc)
    {
        var manager = new SdCardManager(sdmmc);

        using var sharedManager = new SharedRef<SdCardManager>(manager);
        manager._selfReference.Set(in sharedManager);

        return SharedRef<SdCardManager>.CreateMove(ref sharedManager.Ref);
    }

    public void Dispose()
    {
        if (_detectionEventManager.HasValue)
        {
            _detectionEventManager.Value.Dispose();
            _detectionEventManager.Clear();
        }

        _selfReference.Destroy();
    }

    private void DeactivateIfCardRemoved()
    {
        if (_openCount > 0 && _sdmmc.IsSdCardRemoved(_port))
        {
            _sdmmc.Deactivate(_port);
            _handle++;
            _openCount = 0;
        }
    }

    private bool IsShutDown()
    {
        return _openCount < 0;
    }

    private void InitializeSd()
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_isInitialized)
            return;

        // Missing: Work buffer management

        if (_detectionEventManager.HasValue)
        {
            _detectionEventManager.Value.Dispose();
            _detectionEventManager.Clear();
        }

        _detectionEventManager.Set(new SdCardDetectionEventManager(_port, _sdmmc));

        _isInitialized = true;
    }

    public Result IsInserted(out bool isInserted)
    {
        InitializeSd();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        DeactivateIfCardRemoved();

        isInserted = _sdmmc.IsSdCardInserted(_port);
        return Result.Success;
    }

    public Result IsHandleValid(out bool isValid, SdmmcHandle handle)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();
        isValid = Lock(ref scopedLock.Ref(), handle).IsSuccess();

        return Result.Success;
    }

    public Result OpenDetectionEvent(ref SharedRef<IEventNotifier> outDetectionEvent)
    {
        InitializeSd();

        Result res = _detectionEventManager.Value.CreateDetectionEvent(ref outDetectionEvent);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        InitializeSd();

        using SharedRef<SdCardManager> deviceOperator = SharedRef<SdCardManager>.Create(in _selfReference);

        if (!deviceOperator.HasValue)
            return ResultFs.AllocationMemoryFailedInSdmmcStorageServiceA.Log();

        outDeviceOperator.SetByMove(ref deviceOperator.Ref);

        return Result.Success;
    }

    public Result OpenDevice(ref SharedRef<IStorageDevice> outStorageDevice, ulong attribute)
    {
        using var storageDevice = new SharedRef<IStorageDevice>();

        Result res = OpenDeviceImpl(ref storageDevice.Ref);
        if (res.IsFailure()) return res.Miss();

        outStorageDevice.SetByMove(ref storageDevice.Ref);

        return Result.Success;
    }

    public Result OpenStorage(ref SharedRef<IStorageSf> outStorage, ulong attribute)
    {
        using var storageDevice = new SharedRef<IStorageDevice>();

        Result res = OpenDeviceImpl(ref storageDevice.Ref);
        if (res.IsFailure()) return res.Miss();

        outStorage.SetByMove(ref storageDevice.Ref);

        return Result.Success;
    }

    private Result OpenDeviceImpl(ref SharedRef<IStorageDevice> outStorageDevice)
    {
        InitializeSd();

        Result res = EnsureActivated(out SdmmcHandle handle);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<ISdmmcDeviceManager> manager = SharedRef<ISdmmcDeviceManager>.Create(in _selfReference);

        using SharedRef<SdCardStorageDevice>
            storageDevice = SdCardStorageDevice.CreateShared(ref manager.Ref, handle, _sdmmc);

        if (!storageDevice.HasValue)
            return ResultFs.AllocationMemoryFailedInSdmmcStorageServiceA.Log();

        outStorageDevice.SetByMove(ref storageDevice.Ref);

        return Result.Success;
    }

    private Result EnsureActivated(out SdmmcHandle outHandle)
    {
        UnsafeHelpers.SkipParamInit(out outHandle);

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        DeactivateIfCardRemoved();

        if (IsShutDown())
        {
            outHandle = InvalidHandle;
            return Result.Success;
        }

        // Activate the device if we're the first to open it.
        if (_openCount == 0)
        {
            Result res = SdmmcResultConverter.GetFsResult(_port, _sdmmc.Activate(_port));
            if (res.IsFailure()) return res.Miss();

            // Increment the handle if this is the first time the device has been activated.
            if (_handle == 0)
            {
                _handle++;
            }

            if (_openCount++ >= 0)
            {
                outHandle = _handle;
                return Result.Success;
            }

            outHandle = InvalidHandle;
            return Result.Success;
        }

        // The device has already been activated. Just increment the open count.
        _openCount++;
        outHandle = _handle;
        return Result.Success;
    }

    public Result PutToSleep()
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        if (_isInitialized)
            _sdmmc.PutSdCardToSleep(_port);

        return Result.Success;
    }

    public Result Awaken()
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        if (_isInitialized)
            _sdmmc.AwakenSdCard(_port);

        return Result.Success;
    }

    public Result Shutdown()
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        if (_isInitialized)
        {
            _sdmmc.Deactivate(_port);
            _handle++;
            _openCount = OpenCountFinalized;
        }

        return Result.Success;
    }

    public Result Invalidate()
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        if (_openCount > 0)
        {
            _sdmmc.Deactivate(_port);
            _handle++;
            _openCount = 0;
        }

        return Result.Success;
    }

    public Result Lock(ref UniqueLockRef<SdkMutexType> outLock, SdmmcHandle handle)
    {
        InitializeSd();

        using var scopedLock = new UniqueLockRef<SdkMutexType>(ref _mutex);

        DeactivateIfCardRemoved();

        if (handle == InvalidHandle || _handle != handle)
            return ResultFs.PortSdCardStorageDeviceInvalidated.Log();

        outLock.Set(ref scopedLock.Ref());

        return Result.Success;
    }

    public IStorage GetStorage()
    {
        return _sdStorage;
    }

    public Port GetPort()
    {
        return _port;
    }

    public void NotifyCloseStorageDevice(SdmmcHandle handle)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        if (_handle != handle)
            return;

        if (_openCount > 0)
        {
            _openCount--;

            if (_openCount == 0)
            {
                _sdmmc.Deactivate(_port);
                _handle++;
            }
        }
    }

    public Result Operate(int operationId)
    {
        var operation = (SdCardManagerOperationIdValue)operationId;

        switch (operation)
        {
            case SdCardManagerOperationIdValue.SuspendControl:
            case SdCardManagerOperationIdValue.ResumeControl:
                return ResultFs.StorageDeviceInvalidOperation.Log();

            case SdCardManagerOperationIdValue.SimulateDetectionEventSignaled:
                _detectionEventManager.Value.SignalAll();
                return Result.Success;

            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateIn(InBuffer buffer, long offset, long size, int operationId)
    {
        return ResultFs.NotImplemented.Log();
    }

    public Result OperateOut(out long bytesWritten, OutBuffer buffer, int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWritten);

        return ResultFs.NotImplemented.Log();
    }

    public Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2,
        OutBuffer buffer2, int operationId)
    {
        var operation = (SdCardManagerOperationIdValue)operationId;
        bytesWrittenBuffer1 = 0;
        bytesWrittenBuffer2 = 0;

        switch (operation)
        {
            case SdCardManagerOperationIdValue.GetAndClearErrorInfo:
            {
                if (buffer1.Size < Unsafe.SizeOf<StorageErrorInfo>())
                    return ResultFs.InvalidArgument.Log();

                Result res = GetAndClearSdCardErrorInfo(out buffer1.As<StorageErrorInfo>(), out bytesWrittenBuffer2,
                    buffer2.Buffer);
                if (res.IsFailure()) return res.Miss();

                bytesWrittenBuffer1 = Unsafe.SizeOf<StorageErrorInfo>();

                return Result.Success;
            }

            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateInOut(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer, long offset, long size,
        int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWritten);

        return ResultFs.NotImplemented.Log();
    }

    public Result OperateIn2Out(out long bytesWritten, OutBuffer outBuffer, InBuffer inBuffer1, InBuffer inBuffer2,
        long offset, long size, int operationId)
    {
        UnsafeHelpers.SkipParamInit(out bytesWritten);

        return ResultFs.NotImplemented.Log();
    }

    private Result GetAndClearSdCardErrorInfo(out StorageErrorInfo outStorageErrorInfo, out long outLogSize,
        Span<byte> logBuffer)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        Result res = Common.GetAndClearSdmmcStorageErrorInfo(out outStorageErrorInfo, out outLogSize, logBuffer, _sdmmc);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}