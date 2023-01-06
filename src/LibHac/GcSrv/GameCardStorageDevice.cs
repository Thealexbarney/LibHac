using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Gc;
using LibHac.Os;
using LibHac.Sf;

namespace LibHac.GcSrv;

internal class GameCardStorageDevice : GameCardStorageInterfaceAdapter, IStorageDevice
{
    private SharedRef<IGameCardManager> _manager;
    private GameCardHandle _handle;
    private bool _isSecure;
    private Array16<byte> _cardDeviceId;
    private Array32<byte> _cardImageHash;

    // LibHac additions
    private WeakRef<GameCardStorageDevice> _selfReference;
    private readonly GameCardEmulated _gc;

    private GameCardStorageDevice(GameCardEmulated gc, ref SharedRef<IGameCardManager> manager,
        in SharedRef<IStorage> baseStorage, GameCardHandle handle) : base(in baseStorage)
    {
        _manager = SharedRef<IGameCardManager>.CreateMove(ref manager);
        _handle = handle;
        _isSecure = false;

        _gc = gc;
    }

    private GameCardStorageDevice(GameCardEmulated gc, ref SharedRef<IGameCardManager> manager,
        in SharedRef<IStorage> baseStorage, GameCardHandle handle, bool isSecure, ReadOnlySpan<byte> cardDeviceId,
        ReadOnlySpan<byte> cardImageHash)
        : base(in baseStorage)
    {
        Assert.SdkRequiresEqual(cardDeviceId.Length, Values.GcCardDeviceIdSize);
        Assert.SdkRequiresEqual(cardImageHash.Length, Values.GcCardImageHashSize);

        _manager = SharedRef<IGameCardManager>.CreateMove(ref manager);
        _handle = handle;
        _isSecure = isSecure;

        cardDeviceId.CopyTo(_cardDeviceId.Items);
        cardImageHash.CopyTo(_cardImageHash.Items);

        _gc = gc;
    }

    public static SharedRef<GameCardStorageDevice> CreateShared(GameCardEmulated gc,
        ref SharedRef<IGameCardManager> manager, in SharedRef<IStorage> baseStorage, GameCardHandle handle)
    {
        var storageDevice = new GameCardStorageDevice(gc, ref manager, in baseStorage, handle);

        using var sharedStorageDevice = new SharedRef<GameCardStorageDevice>(storageDevice);
        storageDevice._selfReference.Set(in sharedStorageDevice);

        return SharedRef<GameCardStorageDevice>.CreateMove(ref sharedStorageDevice.Ref);
    }

    public static SharedRef<GameCardStorageDevice> CreateShared(GameCardEmulated gc,
        ref SharedRef<IGameCardManager> manager, in SharedRef<IStorage> baseStorage, GameCardHandle handle,
        bool isSecure, ReadOnlySpan<byte> cardDeviceId, ReadOnlySpan<byte> cardImageHash)
    {
        var storageDevice = new GameCardStorageDevice(gc, ref manager, in baseStorage, handle, isSecure, cardDeviceId,
            cardImageHash);

        using var sharedStorageDevice = new SharedRef<GameCardStorageDevice>(storageDevice);
        storageDevice._selfReference.Set(in sharedStorageDevice);

        return SharedRef<GameCardStorageDevice>.CreateMove(ref sharedStorageDevice.Ref);
    }

    public override void Dispose()
    {
        _manager.Destroy();
        _selfReference.Destroy();

        base.Dispose();
    }

    public Result AcquireReadLock(ref SharedLock<ReaderWriterLock> outLock)
    {
        Result res = _isSecure
            ? _manager.Get.AcquireSecureLock(ref outLock, ref _handle, _cardDeviceId, _cardImageHash)
            : _manager.Get.AcquireReadLock(ref outLock, _handle);

        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public Result AcquireWriteLock(ref UniqueLock<ReaderWriterLock> outLock)
    {
        return _manager.Get.AcquireWriteLock(ref outLock).Ret();
    }

    private Result HandleGameCardAccessResultImpl(Result result)
    {
        return _manager.Get.HandleGameCardAccessResult(result);
    }

    public Result HandleGameCardAccessResult(Result result)
    {
        if (result.IsSuccess())
            return Result.Success;

        using var writeLock = new UniqueLock<ReaderWriterLock>();
        Result res = AcquireWriteLock(ref writeLock.Ref());
        if (res.IsFailure()) return res.Miss();

        return HandleGameCardAccessResultImpl(result).Ret();
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
        using var readLock = new SharedLock<ReaderWriterLock>();

        Result res = AcquireReadLock(ref readLock.Ref());
        if (res.IsFailure()) return res.Miss();

        using SharedRef<GameCardStorageDevice> storageDevice =
            SharedRef<GameCardStorageDevice>.Create(in _selfReference);

        using var deviceOperator =
            new SharedRef<GameCardDeviceOperator>(new GameCardDeviceOperator(ref storageDevice.Ref, _gc));

        if (!deviceOperator.HasValue)
            return ResultFs.AllocationMemoryFailedInGameCardManagerG.Log();

        outDeviceOperator.SetByMove(ref deviceOperator.Ref);

        return Result.Success;
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

        return HandleGameCardAccessResult(resultGetSize).Ret();
    }
}