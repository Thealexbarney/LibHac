using System;
using System.IO;

namespace LibHac.Streams
{
    public class SectorStream : Stream
    {
        private readonly Stream _baseStream;
        private readonly long _offset;
        private readonly int _maxBufferSize;
        private readonly bool _keepOpen;

        /// <summary>
        /// The size of the sectors.
        /// </summary>
        public int SectorSize { get; }

        /// <summary>
        /// The number of sectors in the stream.
        /// </summary>
        public int SectorCount { get; }

        /// <summary>
        /// The maximum number of sectors that can be read or written in a single operation.
        /// </summary>
        public int MaxSectors { get; }

        /// <summary>
        /// The current sector this stream is at
        /// </summary>
        protected long CurrentSector { get; private set; }

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream to read/write from</param>
        /// <param name="sectorSize">The size of the sectors to read/write</param>
        public SectorStream(Stream baseStream, int sectorSize)
            : this(baseStream, sectorSize, 1, 0)
        {
        }

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream to read/write from</param>
        /// <param name="sectorSize">The size of the sectors to read/write</param>
        /// <param name="maxSectors">The maximum number of sectors to read/write at once</param>
        /// <param name="offset">Offset to start counting sectors</param>
        public SectorStream(Stream baseStream, int sectorSize, int maxSectors, long offset)
            : this(baseStream, sectorSize, maxSectors, offset, false)
        {
        }

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream to read/write from</param>
        /// <param name="sectorSize">The size of the sectors to read/write</param>
        /// <param name="maxSectors">The maximum number of sectors to read/write at once</param>
        /// <param name="offset">Offset to start counting sectors</param>
        /// <param name="keepOpen">Should this stream leave the base stream open when disposed?</param>
        public SectorStream(Stream baseStream, int sectorSize, int maxSectors, long offset, bool keepOpen)
        {
            SectorSize = sectorSize;
            _baseStream = baseStream;
            MaxSectors = maxSectors;
            _offset = offset;
            _keepOpen = keepOpen;
            _maxBufferSize = MaxSectors * SectorSize;
            baseStream.Position = offset;

            SectorCount = (int)Util.DivideByRoundUp(_baseStream.Length - _offset, sectorSize);
        }

        public override void Flush()
        {
            _baseStream.Flush();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateSize(count);
            int bytesRead = _baseStream.Read(buffer, offset, count);
            CurrentSector += bytesRead / SectorSize;
            return bytesRead;
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
            ValidateSize(count);
            int toWrite = (int)Math.Min(count, Length - Position);

            _baseStream.Write(buffer, offset, toWrite);
            CurrentSector += count / SectorSize;
        }

        public override bool CanRead => _baseStream.CanRead;
        public override bool CanSeek => _baseStream.CanSeek;
        public override bool CanWrite => _baseStream.CanWrite;
        public override long Length => _baseStream.Length - _offset;
        public override long Position
        {
            get => _baseStream.Position - _offset;
            set
            {
                ValidateSizeMultiple(value);
                _baseStream.Position = value + _offset;
                CurrentSector = value / SectorSize;
            }
        }

        /// <summary>
        /// Validates that the size is a multiple of the sector size and smaller than the max buffer size
        /// </summary>
        protected void ValidateSize(long value)
        {
            ValidateSizeMultiple(value);

            if (value > _maxBufferSize)
                throw new ArgumentException($"Value cannot be greater than {_maxBufferSize}");
        }

        /// <summary>
        /// Validates that the size is a multiple of the sector size
        /// </summary>
        protected void ValidateSizeMultiple(long value)
        {
            if (value < 0)
                throw new ArgumentException("Value must be non-negative");
            if (value % SectorSize != 0)
                throw new ArgumentException($"Value must be a multiple of {SectorSize}");
        }

        protected override void Dispose(bool disposing)
        {
            if (!_keepOpen)
            {
                _baseStream.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
