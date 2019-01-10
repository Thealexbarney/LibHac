using System;

namespace LibHac.IO
{
    public interface IFile : IDisposable
    {
        OpenMode Mode { get; }
        int Read(Span<byte> destination, long offset);
        void Write(ReadOnlySpan<byte> source, long offset);
        void Flush();
        long GetSize();
        void SetSize(long size);
    }
}