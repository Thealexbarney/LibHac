using System;
using System.IO;

namespace LibHac.Savefile
{
    public class DuplexFs : Stream
    {
        private int BlockSize{ get; }
        private Stream BitmapStream { get; }
        private Stream DataA { get; }
        private Stream DataB { get; }
        private DuplexBitmap Bitmap { get; }

        public DuplexFs(Stream bitmap, Stream dataA, Stream dataB, int blockSize)
        {
            if (dataA.Length != dataB.Length)
            {
                throw new InvalidDataException("Both data streams must be the same length");
            }

            BlockSize = blockSize;
            BitmapStream = bitmap;
            DataA = dataA;
            DataB = dataB;
            Bitmap = new DuplexBitmap(BitmapStream, (int)(bitmap.Length * 8));
            Length = dataA.Length;
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = Math.Min(count, Length - Position);
            if (remaining <= 0) return 0;
            int outOffset = offset;
            int totalBytesRead = 0;

            while (remaining > 0)
            {
                int blockNum = (int)(Position / BlockSize);
                int blockPos = (int)(Position % BlockSize);
                int bytesToRead = (int)Math.Min(remaining, BlockSize - blockPos);

                Stream data = Bitmap.Bitmap[blockNum] ? DataB : DataA;
                data.Position = blockNum * BlockSize + blockPos;

                data.Read(buffer, outOffset, bytesToRead);
                outOffset += bytesToRead;
                totalBytesRead += bytesToRead;
                remaining -= bytesToRead;
                Position += bytesToRead;
            }

            return totalBytesRead;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            switch (origin)
            {
                case SeekOrigin.Begin:
                    Position = offset;
                    break;
                case SeekOrigin.Current:
                    Position += offset;
                    break;
                case SeekOrigin.End:
                    Position = Length - offset;
                    break;
            }

            return Position;
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }
        public override long Position { get; set; }
    }
}
