using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Gc;
using LibHac.Os;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.GcSrv;

internal abstract class GameCardStorageInterfaceAdapter : IStorageSf
{
    private SharedRef<IStorage> _baseStorage;

    protected GameCardStorageInterfaceAdapter(ref SharedRef<IStorage> baseStorage)
    {
        _baseStorage = SharedRef<IStorage>.CreateMove(ref baseStorage);
    }

    public virtual void Dispose()
    {
        _baseStorage.Destroy();
    }

    public virtual Result Read(long offset, OutBuffer destination, long size)
    {
        return _baseStorage.Get.Read(offset, destination.Buffer.Slice(0, (int)size)).Ret();
    }

    public virtual Result Write(long offset, InBuffer source, long size)
    {
        return _baseStorage.Get.Write(offset, source.Buffer.Slice(0, (int)size)).Ret();
    }

    public virtual Result Flush()
    {
        return _baseStorage.Get.Flush().Ret();
    }

    public virtual Result SetSize(long size)
    {
        return _baseStorage.Get.SetSize(size).Ret();
    }

    public virtual Result GetSize(out long size)
    {
        return _baseStorage.Get.GetSize(out size).Ret();
    }

    public virtual Result OperateRange(out QueryRangeInfo rangeInfo, int operationId, long offset, long size)
    {
        UnsafeHelpers.SkipParamInit(out rangeInfo);

        return _baseStorage.Get.OperateRange(SpanHelpers.AsByteSpan(ref rangeInfo), (OperationId)operationId, offset,
            size, ReadOnlySpan<byte>.Empty).Ret();
    }
}

internal class GameCardStorageDevice : GameCardStorageInterfaceAdapter, IStorageDevice
{
    private SharedRef<IGameCardManager> _manager;
    private GameCardHandle _handle;
    private bool _isSecure;
    private Array16<byte> _cardDeviceId;
    private Array32<byte> _cardImageHash;

    public GameCardStorageDevice(ref SharedRef<IGameCardManager> manager, ref SharedRef<IStorage> baseStorage,
        GameCardHandle handle) : base(ref baseStorage)
    {
        _manager = SharedRef<IGameCardManager>.CreateMove(ref manager);
        _handle = handle;
        _isSecure = false;
    }

    public GameCardStorageDevice(ref SharedRef<IGameCardManager> manager, ref SharedRef<IStorage> baseStorage,
        GameCardHandle handle, bool isSecure, ReadOnlySpan<byte> cardDeviceId, ReadOnlySpan<byte> cardImageHash)
        : base(ref baseStorage)
    {
        Assert.SdkRequiresEqual(cardDeviceId.Length, Values.GcCardDeviceIdSize);
        Assert.SdkRequiresEqual(cardImageHash.Length, Values.GcCardImageHashSize);

        _manager = SharedRef<IGameCardManager>.CreateMove(ref manager);
        _handle = handle;
        _isSecure = isSecure;

        cardDeviceId.CopyTo(_cardDeviceId.Items);
        cardImageHash.CopyTo(_cardImageHash.Items);
    }

    public override void Dispose()
    {
        _manager.Destroy();

        base.Dispose();
    }

    public Result AcquireReadLock(ref SharedLock<ReaderWriterLock> outLock)
    {
        if (_isSecure)
        {
            Result res = _manager.Get.AcquireSecureLock(ref outLock, ref _handle, _cardDeviceId, _cardImageHash);
            if (res.IsFailure()) return res.Miss();
        }
        else
        {
            Result res = _manager.Get.AcquireReadLock(ref outLock, _handle);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result AcquireWriteLock(ref UniqueLock<ReaderWriterLock> outLock)
    {
        return _manager.Get.AcquireWriteLock(ref outLock).Ret();
    }

    public Result HandleGameCardAccessResult(Result result)
    {
        return _manager.Get.HandleGameCardAccessResult(result);
    }

    public Result GetHandle(out GameCardHandle handle)
    {
        handle = _handle;

        return Result.Success;
    }

    public Result IsHandleValid(out bool isValid)
    {
        using var readLock = new SharedLock<ReaderWriterLock>();
        isValid = _manager.Get.AcquireReadLock(ref readLock.Ref(), _handle).IsSuccess();

        return Result.Success;
    }

    public Result OpenOperator(ref SharedRef<IStorageDeviceOperator> outDeviceOperator)
    {
        throw new NotImplementedException();
    }

    public override Result Read(long offset, OutBuffer destination, long size)
    {
        using var readLock = new SharedLock<ReaderWriterLock>();

        Result res = AcquireReadLock(ref readLock.Ref());
        if (res.IsFailure()) return res.Miss();

        res = base.Read(offset, destination, size);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result Write(long offset, InBuffer source, long size)
    {
        using var readLock = new SharedLock<ReaderWriterLock>();

        Result res = AcquireReadLock(ref readLock.Ref());
        if (res.IsFailure()) return res.Miss();

        res = base.Write(offset, source, size);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        Result resultGetSize;
        UnsafeHelpers.SkipParamInit(out size);

        using (var readLock = new SharedLock<ReaderWriterLock>())
        {
            Result res = AcquireReadLock(ref readLock.Ref());
            if (res.IsFailure()) return res.Miss();

            resultGetSize = base.GetSize(out size);
        }

        if (resultGetSize.IsSuccess())
            return Result.Success;

        using (var writeLock = new UniqueLock<ReaderWriterLock>())
        {
            Result res = AcquireWriteLock(ref writeLock.Ref());
            if (res.IsFailure()) return res.Miss();

            return HandleGameCardAccessResult(resultGetSize).Ret();
        }
    }
}