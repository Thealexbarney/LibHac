using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Converts internal RomFS <see cref="Result"/>s returned by an <see cref="IStorage"/> to external <see cref="Result"/>s. 
/// </summary>
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class RomResultConvertStorage : IStorage
{
    private SharedRef<IStorage> _baseStorage;

    public RomResultConvertStorage(ref readonly SharedRef<IStorage> baseStorage)
    {
        _baseStorage = SharedRef<IStorage>.CreateCopy(in baseStorage);
    }

    public override void Dispose()
    {
        _baseStorage.Destroy();
        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        return RomResultConverter.ConvertRomResult(_baseStorage.Get.Read(offset, destination)).Ret();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return RomResultConverter.ConvertRomResult(_baseStorage.Get.Write(offset, source)).Ret();
    }

    public override Result Flush()
    {
        return RomResultConverter.ConvertRomResult(_baseStorage.Get.Flush()).Ret();
    }

    public override Result SetSize(long size)
    {
        return RomResultConverter.ConvertRomResult(_baseStorage.Get.SetSize(size)).Ret();
    }

    public override Result GetSize(out long size)
    {
        return RomResultConverter.ConvertRomResult(_baseStorage.Get.GetSize(out size)).Ret();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return RomResultConverter
            .ConvertRomResult(_baseStorage.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer)).Ret();
    }
}