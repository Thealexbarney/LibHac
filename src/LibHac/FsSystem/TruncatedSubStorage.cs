using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    /// <summary>
    /// A <see cref="SubStorage"/> that truncates reads and writes that extend past the end of the base storage.
    /// </summary>
    /// <remarks>
    /// When reading and writing from a <see cref="TruncatedSubStorage"/>, the size of the base
    /// storage will be checked. If needed, the size of the requested read/write will be truncated
    /// to stay within the bounds of the base storage.
    /// </remarks>
    public class TruncatedSubStorage : SubStorage
    {
        public TruncatedSubStorage() { }
        public TruncatedSubStorage(SubStorage other) : base(other) { }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            if (destination.Length == 0)
                return Result.Success;

            Result rc = BaseStorage.GetSize(out long baseStorageSize);
            if (rc.IsFailure()) return rc;

            long availableSize = baseStorageSize - offset;
            long sizeToRead = Math.Min(destination.Length, availableSize);

            return base.DoRead(offset, destination.Slice(0, (int)sizeToRead));
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            if (source.Length == 0)
                return Result.Success;

            Result rc = BaseStorage.GetSize(out long baseStorageSize);
            if (rc.IsFailure()) return rc;

            long availableSize = baseStorageSize - offset;
            long sizeToWrite = Math.Min(source.Length, availableSize);

            return base.DoWrite(offset, source.Slice(0, (int)sizeToWrite));
        }
    }
}
