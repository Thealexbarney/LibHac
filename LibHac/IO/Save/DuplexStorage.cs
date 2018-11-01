using System;

namespace LibHac.IO.Save
{
    public class DuplexStorage : Storage
    {
        private int BlockSize { get; }
        private Storage BitmapStorage { get; }
        private Storage DataA { get; }
        private Storage DataB { get; }
        private DuplexBitmap Bitmap { get; }

        public DuplexStorage(Storage dataA, Storage dataB, Storage bitmap, int blockSize)
        {
            DataA = dataA;
            DataB = dataB;
            BitmapStorage = bitmap;
            BlockSize = blockSize;

            Bitmap = new DuplexBitmap(BitmapStorage.AsStream(), (int)(bitmap.Length * 8));
            Length = DataA.Length;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                int bytesToRead = Math.Min(remaining, BlockSize - blockPos);

                Storage data = Bitmap.Bitmap[blockNum] ? DataB : DataA;

                int bytesRead = data.Read(destination.Slice(outPos, bytesToRead), inPos);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;
            }

            return outPos;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
        }

        public override void Flush()
        {
            BitmapStorage?.Flush();
            DataA?.Flush();
            DataB?.Flush();
        }

        public override long Length { get; }
    }
}
