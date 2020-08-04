using System;

namespace LibHac.FsService
{
    public interface ISaveDataInfoReader : IDisposable
    {
        Result Read(out long readCount, Span<byte> saveDataInfoBuffer);
    }
}