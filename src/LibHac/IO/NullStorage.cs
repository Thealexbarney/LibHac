using System;

namespace LibHac.IO
{
    /// <summary>
    /// An <see cref="IStorage"/> that returns all zeros when read, and does nothing on write.
    /// </summary>
    public class NullStorage : StorageBase
    {
        public NullStorage() { }
        public NullStorage(long length) => Length = length;

        public override long Length { get; }
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
    }
}
