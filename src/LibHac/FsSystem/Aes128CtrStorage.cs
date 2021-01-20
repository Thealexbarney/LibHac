using System;
using System.Buffers;
using System.Buffers.Binary;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class Aes128CtrStorage : SectorStorage
    {
        private const int BlockSize = 0x10;

        private readonly long _counterOffset;
        private readonly Aes128CtrTransform _decryptor;

        protected readonly byte[] Counter;
        private readonly object _locker = new object();

        public Aes128CtrStorage(IStorage baseStorage, byte[] key, byte[] counter, bool leaveOpen)
            : base(baseStorage, BlockSize, leaveOpen)
        {
            if (key == null) throw new NullReferenceException(nameof(key));
            if (key.Length != BlockSize) throw new ArgumentException(nameof(key), $"Key must be {BlockSize} bytes long");
            if (counter == null) throw new NullReferenceException(nameof(counter));
            if (counter.Length != BlockSize) throw new ArgumentException(nameof(counter), $"Counter must be {BlockSize} bytes long");

            // Make the stream seekable by remembering the initial counter value
            for (int i = 0; i < 8; i++)
            {
                _counterOffset |= (long)counter[0xF - i] << (4 + i * 8);
            }

            _decryptor = new Aes128CtrTransform(key, counter);
            Counter = _decryptor.Counter;
        }

        /// <summary>
        /// Creates a new AES storage
        /// </summary>
        /// <param name="baseStorage">The input <see cref="IStorage"/>.</param>
        /// <param name="key">The decryption key.</param>
        /// <param name="counterOffset">Offset to add to the counter.</param>
        /// <param name="counterHi">The value of the upper 64 bits of the counter. Can be null.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave the storage open after the <see cref="Aes128CtrStorage"/> object is disposed; otherwise, <see langword="false"/>.</param>
        public Aes128CtrStorage(IStorage baseStorage, byte[] key, long counterOffset, byte[] counterHi, bool leaveOpen)
            : base(baseStorage, BlockSize, leaveOpen)
        {
            if (key == null) throw new NullReferenceException(nameof(key));
            if (key.Length != BlockSize) throw new ArgumentException(nameof(key), $"Key must be {BlockSize} bytes long");

            byte[] initialCounter = new byte[BlockSize];
            if (counterHi != null)
            {
                Array.Copy(counterHi, initialCounter, 8);
            }

            _counterOffset = counterOffset;

            _decryptor = new Aes128CtrTransform(key, initialCounter);
            Counter = _decryptor.Counter;
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            Result rc = base.DoRead(offset, destination);
            if (rc.IsFailure()) return rc;

            lock (_locker)
            {
                UpdateCounter(_counterOffset + offset);
                _decryptor.TransformBlock(destination);
            }

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            byte[] encrypted = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                Span<byte> encryptedSpan = encrypted.AsSpan(0, source.Length);
                source.CopyTo(encryptedSpan);

                lock (_locker)
                {
                    UpdateCounter(_counterOffset + offset);
                    _decryptor.TransformBlock(encryptedSpan);
                }

                Result rc = base.DoWrite(offset, encryptedSpan);
                if (rc.IsFailure()) return rc;
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(encrypted);
            }

            return Result.Success;
        }

        private void UpdateCounter(long offset)
        {
            ulong off = (ulong)offset >> 4;
            for (uint j = 0; j < 0x7; j++)
            {
                Counter[0x10 - j - 1] = (byte)(off & 0xFF);
                off >>= 8;
            }

            // Because the value stored in the counter is offset >> 4, the top 4 bits 
            // of byte 8 need to have their original value preserved
            Counter[8] = (byte)((Counter[8] & 0xF0) | (int)(off & 0x0F));
        }

        public static byte[] CreateCounter(ulong hiBytes, long offset)
        {
            byte[] counter = new byte[0x10];

            BinaryPrimitives.WriteUInt64BigEndian(counter, hiBytes);
            BinaryPrimitives.WriteInt64BigEndian(counter.AsSpan(8), offset / 0x10);

            return counter;
        }
    }
}
