using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Os;
using LibHac.Sdmmc;
using LibHac.Sf;
using MmcPartition = LibHac.Sdmmc.MmcPartition;

namespace LibHac.SdmmcSrv;

internal class MmcPartitionStorageDevice : IDisposable
{
    private SharedRef<ISdmmcDeviceManager> _manager;
    private SdmmcHandle _handle;
    private MmcPartition _partition;

    public MmcPartitionStorageDevice(ref SharedRef<ISdmmcDeviceManager> manager, SdmmcHandle handle, MmcPartition partition)
    {
        _manager = SharedRef<ISdmmcDeviceManager>.CreateMove(ref manager);
        _handle = handle;
        _partition = partition;
    }

    public void Dispose() { }

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
        throw new NotImplementedException();
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

// The Mmc*PartitionStorageDevice classes inherit both from SdmmcStorageInterfaceAdapter and MmcPartitionStorageDevice
// Because C# doesn't have multiple inheritance, we make a copy of the SdmmcStorageInterfaceAdapter class that inherits
// from MmcPartitionStorageDevice. This class must mirror any changes made to SdmmcStorageInterfaceAdapter.
internal class MmcPartitionStorageDeviceInterfaceAdapter : MmcPartitionStorageDevice, IStorageDevice
{
    private IStorage _baseStorage;

    public MmcPartitionStorageDeviceInterfaceAdapter(IStorage baseStorage, ref SharedRef<ISdmmcDeviceManager> manager,
        SdmmcHandle handle, MmcPartition partition) : base(ref manager, handle, partition)
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

internal class MmcUserDataPartitionStorageDevice : MmcPartitionStorageDeviceInterfaceAdapter
{
    public MmcUserDataPartitionStorageDevice(ref SharedRef<ISdmmcDeviceManager> manager, SdmmcHandle handle)
        : base(manager.Get.GetStorage(), ref manager, handle, MmcPartition.UserData)
    { }

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

internal class MmcBootPartitionStorageDevice : MmcPartitionStorageDeviceInterfaceAdapter
{
    private SdmmcApi _sdmmc;

    public MmcBootPartitionStorageDevice(ref SharedRef<ISdmmcDeviceManager> manager, Fs.MmcPartition partition,
        SdmmcHandle handle, SdmmcApi sdmmc) : base(manager.Get.GetStorage(), ref manager, handle, GetPartition(partition))
    {
        _sdmmc = sdmmc;
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

        Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(GetPort(), GetPartition()));

        try
        {
            base.Read(offset, destination, size);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(GetPort(), MmcPartition.UserData));
        }
    }

    public override Result Write(long offset, InBuffer source, long size)
    {
        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(GetPort(), GetPartition()));

        try
        {
            base.Write(offset, source, size);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(GetPort(), MmcPartition.UserData));
        }
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var scopedLock = new UniqueLockRef<SdkMutexType>();

        Result res = Lock(ref scopedLock.Ref());
        if (res.IsFailure()) return res.Miss();

        Port port = GetPort();

        Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(port, GetPartition()));

        try
        {
            res = SdmmcResultConverter.GetFsResult(port, _sdmmc.GetMmcBootPartitionCapacity(out uint numSectors, port));
            if (res.IsFailure()) return res.Miss();

            size = numSectors * SdmmcApi.SectorSize;

            return Result.Success;
        }
        finally
        {
            Abort.DoAbortUnlessSuccess(_sdmmc.SelectMmcPartition(GetPort(), MmcPartition.UserData));
        }
    }
}