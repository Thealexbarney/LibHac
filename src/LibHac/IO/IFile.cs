using System;

namespace LibHac.IO
{
    public interface IFile
    {
        void Read(Span<byte> destination, long offset);
        void Write(ReadOnlySpan<byte> source, long offset);
        void Flush();
        long GetSize();
        long SetSize();
    }
}