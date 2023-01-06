using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Gc;
using LibHac.Sf;
using static LibHac.Gc.Values;
using static LibHac.GcSrv.GameCardDeviceOperator;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.GcSrv;

/// <summary>
/// Provides an <see cref="IStorage"/> interface for reading from the game card.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
internal class ReadOnlyGameCardStorage : IStorage
{
    private SharedRef<IGameCardManager> _deviceManager;

    // LibHac additions
    private readonly IGcApi _gc;

    public ReadOnlyGameCardStorage(ref SharedRef<IGameCardManager> deviceManger, IGcApi gc)
    {
        _deviceManager = SharedRef<IGameCardManager>.CreateMove(ref deviceManger);
        _gc = gc;
    }

    public override void Dispose()
    {
        _deviceManager.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Assert.SdkRequiresAligned(offset, GcPageSize);
        Assert.SdkRequiresAligned(destination.Length, GcPageSize);

        if (destination.Length == 0)
            return Result.Success;

        // Missing: Allocate a device buffer if the destination buffer is not one

        return _gc.Read(destination, BytesToPages(offset), BytesToPages(destination.Length)).Ret();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return ResultFs.UnsupportedWriteForReadOnlyGameCardStorage.Log();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result res = _gc.GetCardStatus(out GameCardStatus status);
        if (res.IsFailure()) return res.Miss();

        size = status.CardSize;
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForReadOnlyGameCardStorage.Log();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.InvalidateCache:
                return Result.Success;
            case OperationId.QueryRange:
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer).Clear();

                return Result.Success;
            default:
                return ResultFs.UnsupportedOperateRangeForReadOnlyGameCardStorage.Log();
        }
    }
}

/// <summary>
/// Provides an <see cref="IStorage"/> interface for writing to the game card.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
internal class WriteOnlyGameCardStorage : IStorage
{
    private SharedRef<IGameCardManager> _deviceManager;

    // LibHac additions
    private readonly IGcApi _gc;

    public WriteOnlyGameCardStorage(ref SharedRef<IGameCardManager> deviceManger, IGcApi gc)
    {
        _deviceManager = SharedRef<IGameCardManager>.CreateMove(ref deviceManger);
        _gc = gc;
    }

    public override void Dispose()
    {
        _deviceManager.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        return ResultFs.UnsupportedReadForWriteOnlyGameCardStorage.Log();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        Assert.SdkRequiresAligned(offset, GcPageSize);
        Assert.SdkRequiresAligned(source.Length, GcPageSize);

        if (source.Length == 0)
            return Result.Success;

        // Missing: Allocate a device buffer if the destination buffer is not one

        return _gc.Writer.Write(source, BytesToPages(offset), BytesToPages(source.Length)).Ret();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result res = _gc.Writer.GetCardAvailableRawSize(out long gameCardSize);
        if (res.IsFailure()) return res.Miss();

        size = gameCardSize + GcCardKeyAreaSize;
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedSetSizeForWriteOnlyGameCardStorage.Log();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return ResultFs.NotImplemented.Log();
    }
}

/// <summary>
/// An adapter that provides an <see cref="IStorageSf"/> interface for a <see cref="IStorage"/>.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
internal abstract class GameCardStorageInterfaceAdapter : IStorageSf
{
    private SharedRef<IStorage> _baseStorage;

    protected GameCardStorageInterfaceAdapter(in SharedRef<IStorage> baseStorage)
    {
        _baseStorage = SharedRef<IStorage>.CreateCopy(in baseStorage);
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