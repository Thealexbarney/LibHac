using System;

namespace LibHac.IO
{
    public class Aes128XtsStorage : SectorStorage
    {
        private const int BlockSize = 0x10;

        private readonly byte[] _tempBuffer;

        private Aes128XtsTransform _decryptor;
        private Aes128XtsTransform _encryptor;

        private readonly byte[] _key1;
        private readonly byte[] _key2;

        public Aes128XtsStorage(IStorage baseStorage, Span<byte> key, int sectorSize, bool leaveOpen)
            : base(baseStorage, sectorSize, leaveOpen)
        {
            if (key == null) throw new NullReferenceException(nameof(key));
            if (key.Length != BlockSize * 2) throw new ArgumentException(nameof(key), $"Key must be {BlockSize * 2} bytes long");

            _tempBuffer = new byte[sectorSize];
            _key1 = key.Slice(0, BlockSize).ToArray();
            _key2 = key.Slice(BlockSize, BlockSize).ToArray();

            Length = baseStorage.Length;
        }

        public Aes128XtsStorage(IStorage baseStorage, Span<byte> key1, Span<byte> key2, int sectorSize, bool leaveOpen)
            : base(baseStorage, sectorSize, leaveOpen)
        {
            if (key1 == null) throw new NullReferenceException(nameof(key1));
            if (key2 == null) throw new NullReferenceException(nameof(key2));
            if (key1.Length != BlockSize || key1.Length != BlockSize) throw new ArgumentException($"Keys must be {BlockSize} bytes long");

            _tempBuffer = new byte[sectorSize];
            _key1 = key1.ToArray();
            _key2 = key2.ToArray();

            Length = baseStorage.Length;
        }

        public override long Length { get; }
        protected override void ReadImpl(Span<byte> destination, long offset)
        {
            int size = destination.Length;
            long sectorIndex = offset / SectorSize;

            if (_decryptor == null) _decryptor = new Aes128XtsTransform(_key1, _key2, true);

            base.ReadImpl(_tempBuffer.AsSpan(0, size), offset);

            _decryptor.TransformBlock(_tempBuffer, 0, size, (ulong)sectorIndex);
            _tempBuffer.AsSpan(0, size).CopyTo(destination);
        }

        protected override void WriteImpl(ReadOnlySpan<byte> source, long offset)
        {
            int size = source.Length;
            long sectorIndex = offset / SectorSize;

            if (_encryptor == null) _encryptor = new Aes128XtsTransform(_key1, _key2, false);

            source.CopyTo(_tempBuffer);
            _encryptor.TransformBlock(_tempBuffer, 0, size, (ulong)sectorIndex);

            base.WriteImpl(_tempBuffer.AsSpan(0, size), offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }
    }
}
