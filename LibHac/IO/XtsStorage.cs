using System;
using LibHac.Streams;
using LibHac.XTSSharp;

namespace LibHac.IO
{
    public class XtsStorage : Storage
    {
        private Storage BaseStorage { get; }
        private Storage Storage { get; }

        public XtsStorage(Storage baseStorage, byte[] key, int sectorSize)
        {
            BaseStorage = baseStorage;
            Length = BaseStorage.Length;

            Xts xts = XtsAes128.Create(key);
            var stream = new RandomAccessSectorStream(new XtsSectorStream(BaseStorage.AsStream(), xts, sectorSize));
            Storage = new StreamStorage(stream, false);
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            return Storage.Read(destination, offset);
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            Storage.Write(source, offset);
        }

        public override void Flush()
        {
            Storage.Flush();
            BaseStorage.Flush();
        }

        public override long Length { get; }
    }
}
