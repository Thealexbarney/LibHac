using System;

namespace LibHac.FsSystem
{
    /// <summary>
    /// An <see cref="IStorage"/> that returns all zeros when read, and does nothing on write.
    /// </summary>
    public class NullStorage : StorageBase
    {
        public NullStorage() { }
        public NullStorage(long length) => _length = length;

        private long _length;

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            destination.Clear();
            return Result.Success;
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            return Result.Success;
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        public override Result GetSize(out long size)
        {
            size = _length;
            return Result.Success;
        }
    }
}
