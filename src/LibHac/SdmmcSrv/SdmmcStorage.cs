using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sdmmc;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.SdmmcSrv;

internal class SdmmcStorage : IStorage
{
    private Port _port;

    // LibHac additions
    private SdmmcApi _sdmmc;

    public SdmmcStorage(Port port, SdmmcApi sdmmc)
    {
        _port = port;
        _sdmmc = sdmmc;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public override Result Flush()
    {
        throw new NotImplementedException();
    }

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

internal class SdmmcStorageInterfaceAdapter : IStorageSf
{
    private IStorage _baseStorage;

    public SdmmcStorageInterfaceAdapter(IStorage baseStorage)
    {
        _baseStorage = baseStorage;
    }

    public virtual void Dispose() { }

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