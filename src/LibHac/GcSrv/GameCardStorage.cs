using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Gc;
using static LibHac.Gc.Values;

namespace LibHac.GcSrv;

internal class ReadOnlyGameCardStorage : IStorage
{
    private SharedRef<IGameCardManager> _deviceManager;

    // LibHac additions
    private readonly GameCardDummy _gc;

    public ReadOnlyGameCardStorage(ref SharedRef<IGameCardManager> deviceManger, GameCardDummy gc)
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

        return _gc.Read(destination, GameCardManager.BytesToPages(offset),
            GameCardManager.BytesToPages(destination.Length)).Ret();
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

        size = status.Size;
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

internal class WriteOnlyGameCardStorage : IStorage
{
    private SharedRef<IGameCardManager> _deviceManager;

    // LibHac additions
    private readonly GameCardDummy _gc;

    public WriteOnlyGameCardStorage(ref SharedRef<IGameCardManager> deviceManger, GameCardDummy gc)
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

        return _gc.Writer.Write(source, GameCardManager.BytesToPages(offset),
            GameCardManager.BytesToPages(source.Length)).Ret();
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