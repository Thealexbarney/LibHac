using System;

namespace LibHac.FsSrv.Sf
{
    public interface IDirectorySf : IDisposable
    {
        Result Read(out long entriesRead, Span<byte> entryBuffer);
        Result GetEntryCount(out long entryCount);
    }
}
