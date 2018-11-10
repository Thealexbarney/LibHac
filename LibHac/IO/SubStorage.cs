using System;

namespace LibHac.IO
{
    public class SubStorage : Storage
    {
        private Storage BaseStorage { get; }
        private long Offset { get; }
        public override long Length { get; }

        public SubStorage(Storage baseStorage, long offset, long length) // todo leaveOpen
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Length = length;
        }

        protected override int ReadImpl(Span<byte> destination, long offset)
        {
            return BaseStorage.Read(destination, offset + Offset);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            BaseStorage.Write(source, offset + Offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }

        public override Storage Slice(long start, long length)
        {
            return BaseStorage.Slice(Offset + start, length);
        }
    }
}
