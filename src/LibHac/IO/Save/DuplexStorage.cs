using System;

namespace LibHac.IO.Save
{
    public class DuplexStorage : StorageBase
    {
        private int BlockSize { get; }
        private IStorage BitmapStorage { get; }
        private IStorage DataA { get; }
        private IStorage DataB { get; }
        private DuplexBitmap Bitmap { get; }

        public DuplexStorage(IStorage dataA, IStorage dataB, IStorage bitmap, int blockSize)
        {
            DataA = dataA;
            DataB = dataB;
            BitmapStorage = bitmap;
            BlockSize = blockSize;

            Bitmap = new DuplexBitmap(BitmapStorage, (int)(bitmap.Length * 8));
            Length = DataA.Length;
        }

        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = destination.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                int bytesToRead = Math.Min(remaining, BlockSize - blockPos);

                IStorage data = Bitmap.Bitmap[blockNum] ? DataB : DataA;

                data.Read(destination.Slice(outPos, bytesToRead), inPos);

                outPos += bytesToRead;
                inPos += bytesToRead;
                remaining -= bytesToRead;
            }
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = source.Length;

            while (remaining > 0)
            {
                int blockNum = (int)(inPos / BlockSize);
                int blockPos = (int)(inPos % BlockSize);

                int bytesToWrite = Math.Min(remaining, BlockSize - blockPos);

                IStorage data = Bitmap.Bitmap[blockNum] ? DataB : DataA;

                data.Write(source.Slice(outPos, bytesToWrite), inPos);

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }
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
