using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;
using IStorage = LibHac.FsSrv.Sf.IStorage;
using MmcPartition = LibHac.Fs.MmcPartition;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Manages the state of the internal MMC and allows reading and writing the MMC storage.
/// </summary>
/// <remarks><para>This class implements the <see cref="IStorageDeviceOperator"/> interface, and all available
/// operations are listed in <see cref="MmcManagerOperationIdValue"/>.</para>
/// <para>Based on nnSdk 16.2.0 (FS 16.0.0)</para></remarks>
internal class MmcManager : IStorageDeviceManager, IStorageDeviceOperator, ISdmmcDeviceManager
{
    private const SdmmcHandle MmcHandle = 1;

    private readonly Port _port;
    private bool _isInitialized;
    private bool _isActivated;
    private SdkMutex _mutex;
    private readonly SdmmcStorage _sdmmcStorage;
    private PatrolReader _patrolReader;

    // LibHac additions
    private WeakRef<MmcManager> _selfReference;
    private readonly SdmmcApi _sdmmc;

    private MmcManager(SdmmcApi sdmmc)
    {
        // Missing: An optional parameter with the device address space info is passed in and stored in the MmcManager.

        _port = Port.Mmc0;
        _mutex = new SdkMutex();

        _sdmmcStorage = new SdmmcStorage(_port, sdmmc);
        _patrolReader = new PatrolReader(_mutex, sdmmc);

        _isInitialized = false;
        _isActivated = false;

        // Missing: Setting the device address space

        _sdmmc = sdmmc;
    }

    public static SharedRef<MmcManager> CreateShared(SdmmcApi sdmmc)
    {
        var manager = new MmcManager(sdmmc);

        using var sharedManager = new SharedRef<MmcManager>(manager);
        manager._selfReference.Set(in sharedManager);

        return SharedRef<MmcManager>.CreateMove(ref sharedManager.Ref);
    }

    public void Dispose()
    {
        _patrolReader?.Dispose();
        _patrolReader = null;

        _selfReference.Destroy();
    }

    private Result ActivateMmc()
    {
        InitializeMmc();

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_isActivated)
            return Result.Success;

        Result res = SdmmcResultConverter.GetFsResult(_port, _sdmmc.Activate(_port));
        if (res.IsFailure()) return res.Miss();

        _patrolReader.Start();
        _isActivated = true;

        return Result.Success;
    }

    private void FinalizeMmc()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_isInitialized)
            _sdmmc.Finalize(_port);
    }

    private void InitializeMmc()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_isInitialized)
            return;

        _sdmmc.Initialize(_port);

        // Missing: Register the device buffer
        // Missing: Allocate work buffer

        _isInitialized = true;
    }

    public Result IsInserted(out bool isInserted)
    {
        isInserted = true;

        return Result.Success;
    }

    public Result IsHandleValid(out bool isValid, SdmmcHandle handle)
    {
        UnsafeHelpers.SkipParamInit(out isValid);

        using var scopedLock = new UniqueLockRef<SdkMutexType>();
        isValid = Lock(ref scopedLock.Ref(), handle).IsSuccess();

        return Result.Success;
    }

    public Result OpenDetectionEvent(ref SharedRef<IEventNotifier> outDetectionEvent)
    {
        return ResultFs.UnsupportedOperation.Log();
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        Result res = ActivateMmc();
        if (res.IsFailure()) return res.Miss();

        using SharedRef<MmcManager> deviceOperator = SharedRef<MmcManager>.Create(in _selfReference);

        if (!deviceOperator.HasValue)
            return ResultFs.AllocationMemoryFailedInSdmmcStorageServiceA.Log();

        outDeviceOperator.SetByMove(ref deviceOperator.Ref);

        return Result.Success;
    }

    public Result OpenDevice(ref SharedRef<IStorageDevice> outStorageDevice, ulong attribute)
    {
        using var storageDevice = new SharedRef<IStorageDevice>();

        Result res = OpenDeviceImpl(ref storageDevice.Ref, attribute);
        if (res.IsFailure()) return res.Miss();

        outStorageDevice.SetByMove(ref storageDevice.Ref);

        return Result.Success;
    }

    public Result OpenStorage(ref SharedRef<IStorage> outStorage, ulong attribute)
    {
        using var storageDevice = new SharedRef<IStorageDevice>();

        Result res = OpenDeviceImpl(ref storageDevice.Ref, attribute);
        if (res.IsFailure()) return res.Miss();

        outStorage.SetByMove(ref storageDevice.Ref);

        return Result.Success;
    }

    private Result OpenDeviceImpl(ref SharedRef<IStorageDevice> outStorageDevice, ulong attribute)
    {
        Result res = ActivateMmc();
        if (res.IsFailure()) return res.Miss();

        using var storageDevice = new SharedRef<IStorageDevice>();

        switch ((MmcPartition)attribute)
        {
            case MmcPartition.UserData:
            {
                OpenDeviceUserDataPartition(ref storageDevice.Ref);
                break;
            }
            case MmcPartition.BootPartition1:
            {
                OpenDeviceBootPartition(MmcPartition.BootPartition1, ref storageDevice.Ref);
                break;
            }
            case MmcPartition.BootPartition2:
            {
                OpenDeviceBootPartition(MmcPartition.BootPartition2, ref storageDevice.Ref);
                break;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }

        if (!storageDevice.HasValue)
            return ResultFs.AllocationMemoryFailedInSdmmcStorageServiceA.Log();

        outStorageDevice.SetByMove(ref storageDevice.Ref);

        return Result.Success;
    }

    private void OpenDeviceUserDataPartition(ref SharedRef<IStorageDevice> outStorageDevice)
    {
        using SharedRef<ISdmmcDeviceManager> manager = SharedRef<ISdmmcDeviceManager>.Create(in _selfReference);

        using SharedRef<MmcUserDataPartitionStorageDevice> storageDevice =
            MmcUserDataPartitionStorageDevice.CreateShared(in manager, MmcHandle, _sdmmc);

        outStorageDevice.SetByMove(ref storageDevice.Ref);
    }

    private void OpenDeviceBootPartition(MmcPartition partition, ref SharedRef<IStorageDevice> outStorageDevice)
    {
        using SharedRef<ISdmmcDeviceManager> manager = SharedRef<ISdmmcDeviceManager>.Create(in _selfReference);

        using SharedRef<MmcBootPartitionStorageDevice> storageDevice =
            MmcBootPartitionStorageDevice.CreateShared(partition, in manager, MmcHandle, _sdmmc);

        outStorageDevice.SetByMove(ref storageDevice.Ref);
    }

    public Result PutToSleep()
    {
        if (_isInitialized)
            _patrolReader.Sleep();

        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_isInitialized)
            _sdmmc.PutMmcToSleep(_port);

        return Result.Success;
    }

    public Result Awaken()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (_isInitialized)
        {
            _sdmmc.AwakenMmc(_port);
            _patrolReader.Resume();
        }

        return Result.Success;
    }

    public Result Shutdown()
    {
        if (_isInitialized)
            _patrolReader.Stop();

        FinalizeMmc();

        return Result.Success;
    }

    public Result Invalidate()
    {
        return ResultFs.UnsupportedOperation.Log();
    }

    public Result Lock(ref UniqueLockRef<SdkMutexType> outLock, SdmmcHandle handle)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>(ref _mutex.GetBase());

        if (handle != MmcHandle)
            return ResultFs.PortMmcStorageDeviceInvalidated.Log();

        outLock.Set(ref scopedLock.Ref());

        return Result.Success;
    }

    public Fs.IStorage GetStorage()
    {
        return _sdmmcStorage;
    }

    public Port GetPort()
    {
        return _port;
    }

    public void NotifyCloseStorageDevice(SdmmcHandle handle) { }

    public Result Operate(int operationId)
    {
        var operation = (MmcManagerOperationIdValue)operationId;

        switch (operation)
        {
            case MmcManagerOperationIdValue.SuspendControl:
            case MmcManagerOperationIdValue.ResumeControl:
                return ResultFs.StorageDeviceInvalidOperation.Log();

            case MmcManagerOperationIdValue.SuspendPatrol:
            {
                _patrolReader.Sleep();
                return Result.Success;
            }
            case MmcManagerOperationIdValue.ResumePatrol:
            {
                using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

                _patrolReader.Resume();
                return Result.Success;
            }
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
        bytesWritten = 0;
        var operation = (MmcManagerOperationIdValue)operationId;

        switch (operation)
        {
            case MmcManagerOperationIdValue.GetPatrolCount:
            {
                if (buffer.Size < sizeof(uint))
                    return ResultFs.InvalidArgument.Log();

                Result res = _patrolReader.GetPatrolCount(out buffer.As<uint>());
                if (res.IsFailure()) return res.Miss();

                bytesWritten = sizeof(uint);
                return Result.Success;
            }
            default:
                return ResultFs.InvalidArgument.Log();
        }
    }

    public Result OperateOut2(out long bytesWrittenBuffer1, OutBuffer buffer1, out long bytesWrittenBuffer2,
        OutBuffer buffer2, int operationId)
    {
        bytesWrittenBuffer1 = 0;
        bytesWrittenBuffer2 = 0;
        var operation = (MmcManagerOperationIdValue)operationId;

        switch (operation)
        {
            case MmcManagerOperationIdValue.GetAndClearErrorInfo:
            {
                if (buffer1.Size < Unsafe.SizeOf<StorageErrorInfo>())
                    return ResultFs.InvalidArgument.Log();

                using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

                Result res = Common.GetAndClearSdmmcStorageErrorInfo(out buffer1.As<StorageErrorInfo>(),
                    out bytesWrittenBuffer2, buffer2.Buffer, _sdmmc);
                if (res.IsFailure()) return res.Miss();

                bytesWrittenBuffer1 = Unsafe.SizeOf<StorageErrorInfo>();
                return Result.Success;
            }
            case MmcManagerOperationIdValue.GetAndClearPatrolReadAllocateBufferCount:
            {
                if (buffer1.Size < sizeof(long))
                    return ResultFs.InvalidArgument.Log();

                if (buffer2.Size < sizeof(long))
                    return ResultFs.InvalidArgument.Log();

                _patrolReader.GetAndClearAllocateCount(out buffer1.As<ulong>(), out buffer2.As<ulong>());

                bytesWrittenBuffer1 = sizeof(long);
                bytesWrittenBuffer2 = sizeof(long);

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
}