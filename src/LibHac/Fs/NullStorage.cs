using System;

namespace LibHac.Fs
{
    /// <summary>
    /// An <see cref="IStorage"/> that returns all zeros when read, and does nothing on write.
    /// </summary>
    public class NullStorage : StorageBase
    {
        public NullStorage() { }
        public NullStorage(long length) => _length = length;

        private long _length;

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            destination.Clear();
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
        }

        public override void Flush()
        {
        }

        public override long GetSize() => _length;
    }
}
