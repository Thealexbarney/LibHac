using System;

namespace LibHac.IO
{
    public class Aes128CtrStorage : SectorStorage
    {
        private const int BlockSize = 0x10;

        private readonly long _counterOffset;
        private readonly Aes128CtrTransformStorage _decryptor;

        protected readonly byte[] Counter;

        public Aes128CtrStorage(Storage baseStorage, byte[] key, byte[] counter, bool keepOpen)
            : base(baseStorage, BlockSize, keepOpen)
        {
            if(key == null) throw new NullReferenceException(nameof(key));
            if(key.Length != BlockSize) throw new ArgumentException(nameof(key), $"Key must be {BlockSize} bytes long");
            if(counter == null) throw new NullReferenceException(nameof(counter));
            if(counter.Length != BlockSize) throw new ArgumentException(nameof(counter), $"Counter must be {BlockSize} bytes long");

            // Make the stream seekable by remembering the initial counter value
            for (int i = 0; i < 8; i++)
            {
                _counterOffset |= (long)counter[0xF - i] << (4 + i * 8);
            }

            _decryptor = new Aes128CtrTransformStorage(key, counter);
            Counter = _decryptor.Counter;
        }

        public Aes128CtrStorage(Storage baseStorage, byte[] key, long counterOffset, bool keepOpen, byte[] counterHi = null)
            : base(baseStorage, BlockSize, keepOpen)
        {
            if (key == null) throw new NullReferenceException(nameof(key));
            if (key.Length != BlockSize) throw new ArgumentException(nameof(key), $"Key must be {BlockSize} bytes long");

            var initialCounter = new byte[BlockSize];
            if (counterHi != null)
            {
                Array.Copy(counterHi, initialCounter, 8);
            }

            _counterOffset = counterOffset;

            _decryptor = new Aes128CtrTransformStorage(key, initialCounter);
            Counter = _decryptor.Counter;
        }

        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            int bytesRead = base.ReadSpan(destination, offset);
            if (bytesRead == 0) return 0;

            UpdateCounter(_counterOffset + offset);

            return _decryptor.TransformBlock(destination);
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            throw new NotImplementedException();
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
    }
}
