using System;

namespace LibHac.IO
{
    public class MemoryStorage : Storage
    {
        private byte[] Buffer { get; }
        private int Start { get; }

        public MemoryStorage(byte[] buffer) : this(buffer, 0, buffer.Length)
        {

        }

        public MemoryStorage(byte[] buffer, int index, int count)
        {
            if(buffer == null) throw new NullReferenceException(nameof(buffer));
            if(index < 0) throw new ArgumentOutOfRangeException(nameof(index), "Value must be non-negative.");
            if(count < 0) throw new ArgumentOutOfRangeException(nameof(count), "Value must be non-negative.");
            if(buffer.Length - index < count) throw new ArgumentException("Length, index and count parameters are invalid.");

            Buffer = buffer;
            Start = index;
            Length = count;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            Buffer.AsSpan((int) (Start + offset), destination.Length).CopyTo(destination);
            return destination.Length;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Length { get; }
    }
}
