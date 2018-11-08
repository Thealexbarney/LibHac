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
            if (baseStorage is SubStorage sub)
            {
                BaseStorage = sub.BaseStorage;
                Offset = sub.Offset + offset;
                Length = length;
                return;
            }

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
    }
}
