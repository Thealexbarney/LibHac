using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.FsSrv.Storage.Sf;
using LibHac.Sf;
using IStorageSf = LibHac.FsSrv.Sf.IStorage;

namespace LibHac.Fs.Impl;

/// <summary>
/// An adapter for using an <see cref="IStorageSf"/> service object as an <see cref="IStorage"/>. Used
/// when receiving a Horizon IPC storage object so it can be used as an <see cref="IStorage"/> locally.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
internal class StorageServiceObjectAdapter : IStorage
{
    private SharedRef<IStorageSf> _baseStorage;

    public StorageServiceObjectAdapter(ref SharedRef<IStorageSf> baseStorage)
    {
        _baseStorage = SharedRef<IStorageSf>.CreateMove(ref baseStorage);
    }

    public StorageServiceObjectAdapter(ref SharedRef<IStorageDevice> baseStorage)
    {
        _baseStorage = SharedRef<IStorageSf>.CreateMove(ref baseStorage);
    }

    public override void Dispose()
    {
        _baseStorage.Destroy();
        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        return _baseStorage.Get.Read(offset, new OutBuffer(destination), destination.Length);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return _baseStorage.Get.Write(offset, new InBuffer(source), source.Length);
    }

    public override Result Flush()
    {
        return _baseStorage.Get.Flush();
    }

    public override Result SetSize(long size)
    {
        return _baseStorage.Get.SetSize(size);
    }

    public override Result GetSize(out long size)
    {
        return _baseStorage.Get.GetSize(out size);
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        switch (operationId)
        {
            case OperationId.QueryRange:
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                ref QueryRangeInfo info = ref SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer);

                return _baseStorage.Get.OperateRange(out info, (int)OperationId.QueryRange, offset, size);

            case OperationId.InvalidateCache:
                return _baseStorage.Get.OperateRange(out _, (int)OperationId.InvalidateCache, offset, size);

            default:
                return ResultFs.UnsupportedOperateRangeForStorageServiceObjectAdapter.Log();
        }
    }
}