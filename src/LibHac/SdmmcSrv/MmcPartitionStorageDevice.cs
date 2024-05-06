using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;
using MmcPartition = LibHac.Sdmmc.MmcPartition;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.SdmmcSrv;

/// <summary>
/// Provides base functionality for MMC <see cref="IStorageDevice"/> classes. Derived classes will need to provide
/// methods for reading/writing the MMC storage.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
internal abstract class MmcPartitionStorageDevice : IDisposable
{
    private SharedRef<ISdmmcDeviceManager> _manager;
    private readonly SdmmcHandle _handle;
    private readonly MmcPartition _partition;

    // LibHac addition
    protected WeakRef<MmcPartitionStorageDevice> SelfReference;
    protected readonly SdmmcApi Sdmmc;

    protected MmcPartitionStorageDevice(MmcPartition partition, ref readonly SharedRef<ISdmmcDeviceManager> manager,
        SdmmcHandle handle, SdmmcApi sdmmc)
    {
        _partition = partition;
        _manager = SharedRef<ISdmmcDeviceManager>.CreateCopy(in manager);
        _handle = handle;
        Sdmmc = sdmmc;
    }

    public void Dispose()
    {
        _manager.Destroy();
        SelfReference.Destroy();
    }

    public Result GetHandle(out SdmmcHandle handle)
    {
        handle = _handle;
        return Result.Success;
    }

    public Result IsHandleValid(out bool isValid)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();
        isValid = _manager.Get.Lock(ref scopedLock.Ref(), _handle).IsSuccess();

        return Result.Success;
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        using SharedRef<MmcPartitionStorageDevice> storageDevice =
            SharedRef<MmcPartitionStorageDevice>.Create(in SelfReference);

        using var deviceOperator =
            new SharedRef<MmcDeviceOperator>(new MmcDeviceOperator(ref storageDevice.Ref, Sdmmc));

        if (!deviceOperator.HasValue)
            return ResultFs.AllocationMemoryFailedInSdmmcStorageServiceA.Log();

        outDeviceOperator.SetByMove(ref deviceOperator.Ref);

        return Result.Success;
    }

    public Result Lock(ref UniqueLockRef<SdkMutexType> outLock)
    {
        return _manager.Get.Lock(ref outLock.Ref(), _handle).Ret();
    }

    public Port GetPort()
    {
        return _manager.Get.GetPort();
    }

    public MmcPartition GetPartition()
    {
        return _partition;
    }
}

/// <summary>
/// An adapter that directly translates <see cref="IStorageSf"/> sf calls to <see cref="IStorage"/> calls with no checks
/// or validations.
/// </summary>
/// <remarks><para>The Mmc*PartitionStorageDevice classes inherit both from <see cref="SdmmcStorageInterfaceAdapter"/>
/// and <see cref="MmcPartitionStorageDevice"/>. Because C# doesn't have multiple inheritance, we make a copy of the
/// <see cref="SdmmcStorageInterfaceAdapter"/> class that inherits from <see cref="MmcPartitionStorageDevice"/>.
/// This class must mirror any changes made to <see cref="SdmmcStorageInterfaceAdapter"/>.</para>
/// <para>Based on nnSdk 16.2.0 (FS 16.0.0)</para></remarks>
internal abstract class MmcPartitionStorageDeviceInterfaceAdapter : MmcPartitionStorageDevice, IStorageDevice
{
    private readonly IStorage _baseStorage;

    protected MmcPartitionStorageDeviceInterfaceAdapter(IStorage baseStorage, MmcPartition partition,
        ref readonly SharedRef<ISdmmcDeviceManager> manager, SdmmcHandle handle, SdmmcApi sdmmc)
        : base(partition, in manager, handle, sdmmc)
    {
        _baseStorage = baseStorage;
    }

    public virtual Result Read(long offset, OutBuffer destination, long size)
    {
        return _baseStorage.Read(offset, destination.Buffer.Slice(0, (int)size)).Ret();
    }

    public virtual Result Write(long offset, InBuffer source, long size)
    {
        return _baseStorage.Write(offset, source.Buffer.Slice(0, (int)size)).Ret();
    }

    public virtual Result Flush()
    {
        return _baseStorage.Flush().Ret();
    }

    public virtual Result SetSize(long size)
    {
        return _baseStorage.SetSize(size).Ret();
    }

    public virtual Result GetSize(out long size)
    {
        return _baseStorage.GetSize(out size).Ret();
    }

    public virtual Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
    {
        UnsafeHelpers.SkipParamInit(out rangeInfo);

        return _baseStorage.OperateRange(SpanHelpers.AsByteSpan(ref rangeInfo), (OperationId)operationId, offset,
            size, ReadOnlySpan<byte>.Empty).Ret();
    }
}

/// <summary>
/// An <see cref="IStorageDevice"/> that handles interacting with the <see cref="MmcPartition.UserData"/> partition
/// on the internal MMC.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
internal class MmcUserDataPartitionStorageDevice : MmcPartitionStorageDeviceInterfaceAdapter
{
    private MmcUserDataPartitionStorageDevice(ref readonly SharedRef<ISdmmcDeviceManager> manager, SdmmcHandle handle,
        SdmmcApi sdmmc)
        : base(manager.Get.GetStorage(), MmcPartition.UserData, in manager, handle, sdmmc) { }

    public static SharedRef<MmcUserDataPartitionStorageDevice> CreateShared(ref readonly SharedRef<ISdmmcDeviceManager> manager,
        SdmmcHandle handle, SdmmcApi sdmmc)
    {
        var storageDevice = new MmcUserDataPartitionStorageDevice(in manager, handle, sdmmc);

        using var sharedStorageDevice = new SharedRef<MmcUserDataPartitionStorageDevice>(storageDevice);
        storageDevice.SelfReference.Set(in sharedStorageDevice);

        return SharedRef<MmcUserDataPartitionStorageDevice>.CreateMove(ref sharedStorageDevice.Ref);
    }

    public override Result Read(long offset, OutBuffer destination, long size)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        base.Read(offset, destination, size);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result Write(long offset, InBuffer source, long size)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        res = base.Write(offset, source, size);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        res = base.GetSize(out size);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}

/// <summary>
/// An <see cref="IStorageDevice"/> that handles interacting with the <see cref="MmcPartition.BootPartition1"/> and
/// <see cref="MmcPartition.BootPartition2"/> partitions on the internal MMC.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
internal class MmcBootPartitionStorageDevice : MmcPartitionStorageDeviceInterfaceAdapter
{
    private MmcBootPartitionStorageDevice(Fs.MmcPartition partition, ref readonly SharedRef<ISdmmcDeviceManager> manager,
        SdmmcHandle handle, SdmmcApi sdmmc)
        : base(manager.Get.GetStorage(), GetPartition(partition), in manager, handle, sdmmc) { }

    public static SharedRef<MmcBootPartitionStorageDevice> CreateShared(Fs.MmcPartition partition,
        ref readonly SharedRef<ISdmmcDeviceManager> manager, SdmmcHandle handle, SdmmcApi sdmmc)
    {
        var storageDevice = new MmcBootPartitionStorageDevice(partition, in manager, handle, sdmmc);

        using var sharedStorageDevice = new SharedRef<MmcBootPartitionStorageDevice>(storageDevice);
        storageDevice.SelfReference.Set(in sharedStorageDevice);

        return SharedRef<MmcBootPartitionStorageDevice>.CreateMove(ref sharedStorageDevice.Ref);
    }

    private static MmcPartition GetPartition(Fs.MmcPartition partition)
    {
        switch (partition)
        {
            case Fs.MmcPartition.UserData:
                return MmcPartition.UserData;
            case Fs.MmcPartition.BootPartition1:
                return MmcPartition.BootPartition1;
            case Fs.MmcPartition.BootPartition2:
                return MmcPartition.BootPartition2;
            default:
                Abort.UnexpectedDefault();
                return default;
        }
    }

    public override Result Read(long offset, OutBuffer destination, long size)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        Abort.DoAbortUnlessSuccess(Sdmmc.SelectMmcPartition(GetPort(), GetPartition()));

        try
        {
            base.Read(offset, destination, size);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(Sdmmc.SelectMmcPartition(GetPort(), MmcPartition.UserData));
        }
    }

    public override Result Write(long offset, InBuffer source, long size)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        Abort.DoAbortUnlessSuccess(Sdmmc.SelectMmcPartition(GetPort(), GetPartition()));

        try
        {
            base.Write(offset, source, size);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(Sdmmc.SelectMmcPartition(GetPort(), MmcPartition.UserData));
        }
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        Port port = GetPort();

        Abort.DoAbortUnlessSuccess(Sdmmc.SelectMmcPartition(port, GetPartition()));

        try
        {
            res = SdmmcResultConverter.GetFsResult(port, Sdmmc.GetMmcBootPartitionCapacity(out uint numSectors, port));
            if (res.IsFailure()) return res.Miss();

            size = numSectors * SdmmcApi.SectorSize;

            return Result.Success;
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(Sdmmc.SelectMmcPartition(GetPort(), MmcPartition.UserData));
        }
    }
}