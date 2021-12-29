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

    public override Result Read(long offset, Span<byte> destination)
    {
        return _baseStorage.Get.Read(offset, destination);
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return _baseStorage.Get.Write(offset, source);
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
        return _baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}