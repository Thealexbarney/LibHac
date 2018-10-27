using System;

namespace LibHac.IO
{
    public class SubStorage : Storage
    {
        private Storage BaseStorage { get; }
        private long Offset { get; }
        public override long Length { get; }

        public SubStorage(Storage baseStorage, long offset, long length)
        {
            BaseStorage = baseStorage;
            Offset = offset;
            Length = length;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            return BaseStorage.Read(destination, offset + Offset);
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            BaseStorage.Write(source, offset + Offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }
    }
}
