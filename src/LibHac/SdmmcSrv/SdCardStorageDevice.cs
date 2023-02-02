using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;

namespace LibHac.SdmmcSrv;

/// <summary>
/// An <see cref="IStorageDevice"/> that handles interacting with the currently inserted game card.
/// </summary>
/// <remarks>Based on nnSdk 15.3.0 (FS 15.0.0)</remarks>
internal class SdCardStorageDevice : SdmmcStorageInterfaceAdapter, IStorageDevice
{
    private SharedRef<ISdmmcDeviceManager> _manager;
    private readonly SdmmcHandle _handle;

    // LibHac additions
    private WeakRef<SdCardStorageDevice> _selfReference;
    private readonly SdmmcApi _sdmmc;

    private SdCardStorageDevice(ref SharedRef<ISdmmcDeviceManager> manager, SdmmcHandle handle, SdmmcApi sdmmc)
        : base(manager.Get.GetStorage())
    {
        _manager = SharedRef<ISdmmcDeviceManager>.CreateMove(ref manager);
        _handle = handle;
        _sdmmc = sdmmc;
    }

    public static SharedRef<SdCardStorageDevice> CreateShared(ref SharedRef<ISdmmcDeviceManager> manager,
        SdmmcHandle handle, SdmmcApi sdmmc)
    {
        var device = new SdCardStorageDevice(ref manager, handle, sdmmc);

        using var sharedDevice = new SharedRef<SdCardStorageDevice>(device);
        device._selfReference.Set(in sharedDevice);

        return SharedRef<SdCardStorageDevice>.CreateMove(ref sharedDevice.Ref);
    }

    public override void Dispose()
    {
        _manager.Get.NotifyCloseStorageDevice(_handle);
        _manager.Destroy();

        _selfReference.Destroy();

        base.Dispose();
    }

    public Port GetPort()
    {
        return _manager.Get.GetPort();
    }

    public Result GetHandle(out SdmmcHandle handle)
    {
        handle = _handle;
        return Result.Success;
    }

    public Result IsHandleValid(out bool isValid)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();
        isValid = Lock(ref scopedLock.Ref()).IsSuccess();

        return Result.Success;
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        using SharedRef<SdCardStorageDevice> storageDevice = SharedRef<SdCardStorageDevice>.Create(in _selfReference);

        using var deviceOperator =
            new SharedRef<SdCardDeviceOperator>(new SdCardDeviceOperator(ref storageDevice.Ref, _sdmmc));

        if (!deviceOperator.HasValue)
            return ResultFs.AllocationMemoryFailedInSdmmcStorageServiceA.Log();

        outDeviceOperator.SetByMove(ref deviceOperator.Ref);

        return Result.Success;
    }

    public Result Lock(ref UniqueLockRef<SdkMutexType> outLock)
    {
        Result res = _manager.Get.Lock(ref outLock, _handle);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result Read(long offset, OutBuffer destination, long size)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        return base.Read(offset, destination, size).Ret();
    }

    public override Result Write(long offset, InBuffer source, long size)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        return base.Write(offset, source, size).Ret();
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        return base.GetSize(out size).Ret();
    }
}