using System;
using System.IO;
using LibHac.Streams;

namespace LibHac
{
    public class Aes128CtrStream : SectorStream
    {
        private const int CryptChunkSize = 0x4000;
        private const int BlockSize = 0x10;

        private readonly long _counterOffset;
        private readonly byte[] _tempBuffer;
        private readonly Aes128CtrTransform _decryptor;
        protected readonly byte[] Counter;

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream</param>
        /// <param name="key">The decryption key</param>
        /// <param name="counterOffset">Offset to add to the counter</param>
        /// <param name="ctrHi">The value of the upper 64 bits of the counter</param>
        public Aes128CtrStream(Stream baseStream, byte[] key, long counterOffset = 0, byte[] ctrHi = null)
            : this(baseStream, key, 0, baseStream.Length, counterOffset, ctrHi) { }

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream</param>
        /// <param name="key">The decryption key</param>
        /// <param name="counter">The initial counter</param>
        public Aes128CtrStream(Stream baseStream, byte[] key, byte[] counter)
            : base(baseStream, BlockSize)
        {
            _counterOffset = 0;

            // Make the stream seekable by remembering the initial counter value
            if (counter != null)
            {
                for (int i = 0; i < 8; i++)
                {
                    _counterOffset |= (long)counter[0xF - i] << (4 + i * 8);
                }
            }

            Length = baseStream.Length;
            _tempBuffer = new byte[CryptChunkSize];

            _decryptor = new Aes128CtrTransform(key, counter ?? new byte[0x10], CryptChunkSize);
            Counter = _decryptor.Counter;

            baseStream.Position = 0;
        }

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream</param>
        /// <param name="key">The decryption key</param>
        /// <param name="offset">Offset to start at in the input stream</param>
        /// <param name="length">The length of the created stream</param>
        /// <param name="counterOffset">Offset to add to the counter</param>
        /// <param name="ctrHi">The value of the upper 64 bits of the counter</param>
        public Aes128CtrStream(Stream baseStream, byte[] key, long offset, long length, long counterOffset, byte[] ctrHi = null)
            : base(baseStream, BlockSize, CryptChunkSize / BlockSize, offset)
        {
            var initialCounter = new byte[BlockSize];
            if (ctrHi != null)
            {
                Array.Copy(ctrHi, initialCounter, 8);
            }

            _counterOffset = counterOffset;
            Length = length;
            _tempBuffer = new byte[CryptChunkSize];

            _decryptor = new Aes128CtrTransform(key, initialCounter, CryptChunkSize);
            Counter = _decryptor.Counter;
            UpdateCounter(_counterOffset + base.Position);

            baseStream.Position = offset;
        }

        private void UpdateCounter(long offset)
        {
            ulong off = (ulong)offset >> 4;
            for (uint j = 0; j < 0x8; j++)
            {
                Counter[0x10 - j - 1] = (byte)(off & 0xFF);
                off >>= 8;
            }
        }

        public override void Flush()
        {
            throw new NotImplementedException();
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
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }

        public override long Position
        {
            get => base.Position;
            set
            {
                base.Position = value;
                UpdateCounter(_counterOffset + base.Position);
            }
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateSize(count);

            var bytesRead = base.Read(_tempBuffer, 0, count);
            if (bytesRead == 0) return 0;

            return _decryptor.TransformBlock(_tempBuffer, 0, bytesRead, buffer, offset);
        }
    }
}
