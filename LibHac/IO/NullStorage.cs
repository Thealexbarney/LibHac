using System;

namespace LibHac.IO
{
    /// <summary>
    /// A <see cref="Storage"/> that returns all zeros when read, and does nothing on write.
    /// </summary>
    public class NullStorage : Storage
    {
        public NullStorage() { }
        public NullStorage(long length) => Length = length;

        public override long Length { get; }
        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            destination.Clear();
            return destination.Length;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
        }

        public override void Flush()
        {
        }
    }
}
