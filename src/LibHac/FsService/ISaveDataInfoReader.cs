using System;

namespace LibHac.FsService
{
    public interface ISaveDataInfoReader
    {
        Result ReadSaveDataInfo(out long readCount, Span<byte> saveDataInfoBuffer);
    }
}