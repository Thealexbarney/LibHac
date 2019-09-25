using System;

namespace LibHac.Fs
{
    public abstract class StorageBase : IStorage
    {
        public abstract Result Read(long offset, Span<byte> destination);
        public abstract Result Write(long offset, ReadOnlySpan<byte> source);
        public abstract Result Flush();
        public abstract Result SetSize(long size);
        public abstract Result GetSize(out long size);

        public virtual void Dispose() { }

        public static bool IsRangeValid(long offset, long size, long totalSize)
        {
            return size <= totalSize && offset <= totalSize - size;
        }
    }
}
