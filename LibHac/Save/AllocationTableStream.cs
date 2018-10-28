﻿using System;
using System.IO;

namespace LibHac.Save
{
    public class AllocationTableStream : Stream
    {
        private int SegmentPos => (int)(Data.Position - (Iterator.PhysicalBlock * BlockSize));

        private Stream Data { get; }
        private int BlockSize { get; }
        private AllocationTableIterator Iterator { get; }

        public AllocationTableStream(Stream data, AllocationTable table, int blockSize, int initialBlock, long length)
        {
            Data = data;
            BlockSize = blockSize;
            Length = length;
            Iterator = new AllocationTableIterator(table, initialBlock);
            Data.Position = Iterator.PhysicalBlock * BlockSize;
        }

        public override void Flush()
        {
            Data.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            int remaining = count;
            int outOffset = offset;
            int totalBytesRead = 0;

            while (remaining > 0)
            {
                int remainingInSegment = Iterator.CurrentSegmentSize * BlockSize - SegmentPos;
                int bytesToRead = Math.Min(remaining, remainingInSegment);
                int bytesRead = Data.Read(buffer, outOffset, bytesToRead);

                outOffset += bytesRead;
                totalBytesRead += bytesRead;
                remaining -= bytesRead;

                if (SegmentPos >= Iterator.CurrentSegmentSize * BlockSize)
                {
                    if (!Iterator.MoveNext()) return totalBytesRead;
                    Data.Position = Iterator.PhysicalBlock * BlockSize;
                }
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
            int remaining = count;
            int outOffset = offset;

            while (remaining > 0)
            {
                int remainingInSegment = Iterator.CurrentSegmentSize * BlockSize - SegmentPos;
                int bytesToWrite = Math.Min(remaining, remainingInSegment);
                Data.Write(buffer, outOffset, bytesToWrite);

                outOffset += bytesToWrite;
                remaining -= bytesToWrite;

                if (SegmentPos >= Iterator.CurrentSegmentSize * BlockSize)
                {
                    if (!Iterator.MoveNext()) return;
                    Data.Position = Iterator.PhysicalBlock * BlockSize;
                }
            }
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => true;
        public override long Length { get; }

        public override long Position
        {
            get => Iterator.VirtualBlock * BlockSize + (Data.Position - (Iterator.PhysicalBlock * BlockSize));
            set
            {
                long blockIndex = value / BlockSize;

                while (Iterator.VirtualBlock > blockIndex ||
                       Iterator.VirtualBlock + Iterator.CurrentSegmentSize <= blockIndex)
                {
                    if (Iterator.VirtualBlock > blockIndex)
                    {
                        Iterator.MovePrevious();
                    }
                    else
                    {
                        Iterator.MoveNext();
                    }
                }

                long segmentPos = value - (Iterator.VirtualBlock * BlockSize);
                Data.Position = Iterator.PhysicalBlock * BlockSize + segmentPos;
            }
        }

        protected override void Dispose(bool disposing)
        {
            Flush();
            base.Dispose(disposing);
        }
    }
}
