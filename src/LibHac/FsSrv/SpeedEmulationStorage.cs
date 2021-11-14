using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv;

// Todo: Implement
public class SpeedEmulationStorage : IStorage
{
    private SharedRef<IStorage> _baseStorage;

    public SpeedEmulationStorage(ref SharedRef<IStorage> baseStorage, FileSystemServer fsServer)
    {
        _baseStorage = SharedRef<IStorage>.CreateMove(ref baseStorage);
    }

    public override void Dispose()
    {
        _baseStorage.Destroy();
        base.Dispose();
    }

    protected override Result DoRead(long offset, Span<byte> destination)
    {
        return _baseStorage.Get.Read(offset, destination);
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
    {
        return _baseStorage.Get.Write(offset, source);
    }

    protected override Result DoFlush()
    {
        return _baseStorage.Get.Flush();
    }

    protected override Result DoSetSize(long size)
    {
        return _baseStorage.Get.SetSize(size);
    }

    protected override Result DoGetSize(out long size)
    {
        return _baseStorage.Get.GetSize(out size);
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}
