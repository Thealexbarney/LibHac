using System;

namespace LibHac.FsService
{
    public interface ISaveDataInfoReader : IDisposable
    {
        Result ReadSaveDataInfo(out long readCount, Span<byte> saveDataInfoBuffer);
    }
}