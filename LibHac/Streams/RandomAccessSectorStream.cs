using System;
using System.IO;

namespace LibHac.Streams
{
    public class RandomAccessSectorStream : Stream
    {
        private readonly byte[] _buffer;
        private readonly int _bufferSize;
        private readonly SectorStream _baseStream;
        private readonly bool _keepOpen;
        private int _readBytes; // Number of bytes read into buffer
        private int _bufferPos;
        private long _currentSector;
        private bool _bufferDirty;

        public RandomAccessSectorStream(SectorStream baseStream)
            : this(baseStream, true) { }

        public RandomAccessSectorStream(SectorStream baseStream, bool keepOpen)
        {
            _baseStream = baseStream;
            _keepOpen = keepOpen;
            _bufferSize = baseStream.SectorSize * baseStream.MaxSectors;
            _buffer = new byte[_bufferSize];
        }

        private void FillBuffer(long sectorNum)
        {
            WriteSectorIfDirty();

            _currentSector = sectorNum;
            long startPos = sectorNum * _bufferSize;
            if (_baseStream.Position != startPos)
            {
                _baseStream.Position = startPos;
            }

            _readBytes = _baseStream.Read(_buffer, 0, _bufferSize);
        }

        private void WriteSectorIfDirty()
        {
            if (_readBytes == 0 || !_bufferDirty) return;

            _baseStream.Position = _currentSector * _bufferSize;
            _baseStream.Write(_buffer, 0, _bufferSize);

            _readBytes = 0;
            _bufferDirty = false;
        }

        public override void Flush()
        {
            WriteSectorIfDirty();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            long remaining = Math.Min(count, Length - Position);
            if (remaining <= 0) return 0;

            if (_readBytes == 0) FillBuffer(Position / _bufferSize);
            int outOffset = offset;
            int totalBytesRead = 0;

            while (remaining > 0)
            {
                int bytesToRead = (int)Math.Min(remaining, _bufferSize - _bufferPos);

                Buffer.BlockCopy(_buffer, _bufferPos, buffer, outOffset, bytesToRead);

                outOffset += bytesToRead;
                _bufferPos += bytesToRead;
                totalBytesRead += bytesToRead;
                remaining -= bytesToRead;

                if (_bufferPos == _bufferSize)
                {
                    FillBuffer(_currentSector + 1);
                    _bufferPos = 0;
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
                if (_readBytes == 0) FillBuffer(Position / _bufferSize);

                int bytesToWrite = Math.Min(remaining, _readBytes - _bufferPos);

                Buffer.BlockCopy(buffer, outOffset, _buffer, _bufferPos, bytesToWrite);

                outOffset += bytesToWrite;
                _bufferPos += bytesToWrite;
                remaining -= bytesToWrite;
                _bufferDirty = true;

                if (_bufferPos == _bufferSize)
                {
                    WriteSectorIfDirty();
                }
            }
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length;
        public override long Position
        {
            get => _baseStream.Position - _readBytes + _bufferPos;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(nameof(value));

                long sectorNum = value / _bufferSize;

                if (sectorNum != _currentSector)
                {
                    FillBuffer(sectorNum);
                }

                _bufferPos = (int)(value % _bufferSize);

            }
        }

        protected override void Dispose(bool disposing)
        {
            Flush();

            base.Dispose(disposing);

            if (!_keepOpen)
                _baseStream.Dispose();
        }
    }
}
