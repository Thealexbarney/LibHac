using System;

namespace LibHac.IO
{
    public abstract class FileBase : IFile
    {
        public abstract int Read(Span<byte> destination, long offset);
        public abstract void Write(ReadOnlySpan<byte> source, long offset);
        public abstract void Flush();
        public abstract long GetSize();
        public abstract long SetSize();

        protected int GetAvailableSizeAndValidate(ReadOnlySpan<byte> span, long offset)
        {
            long fileLength = GetSize();

            if (span == null) throw new ArgumentNullException(nameof(span));
            if (offset < 0) throw new ArgumentOutOfRangeException(nameof(offset), "Argument must be non-negative.");

            return (int)Math.Min(fileLength - offset, span.Length);
        }
    }
}
