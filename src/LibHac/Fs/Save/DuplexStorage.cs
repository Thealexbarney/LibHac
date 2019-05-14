using System;

namespace LibHac.Fs.Save
{
    public class DuplexStorage : StorageBase
    {
        private int BlockSize { get; }
        private IStorage BitmapStorage { get; }
        private IStorage DataA { get; }
        private IStorage DataB { get; }
        private DuplexBitmap Bitmap { get; }

        private long _length;

        public DuplexStorage(IStorage dataA, IStorage dataB, IStorage bitmap, int blockSize)
        {
            DataA = dataA;
            DataB = dataB;
            BitmapStorage = bitmap;
            BlockSize = blockSize;

            Bitmap = new DuplexBitmap(BitmapStorage, (int)(bitmap.GetSize() * 8));
            _length = DataA.GetSize();
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

        public override long GetSize() => _length;

        public void FsTrim()
        {
            int blockCount = (int)(DataA.GetSize() / BlockSize);

            for (int i = 0; i < blockCount; i++)
            {
                IStorage dataToClear = Bitmap.Bitmap[i] ? DataA : DataB;

                dataToClear.Slice(i * BlockSize, BlockSize).Fill(SaveDataFileSystem.TrimFillValue);
            }
        }
    }
}
