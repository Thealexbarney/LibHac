﻿using System;

namespace LibHac.IO
{
    public class Aes128XtsStorage : SectorStorage
    {
        private const int BlockSize = 0x10;

        private readonly byte[] _tempBuffer;
        private readonly byte[] _tempBuffer2;

        private Aes128XtsTransform _decryptor;
        private Aes128XtsTransform _encryptor;

        private readonly byte[] _key1;
        private readonly byte[] _key2;

        public Aes128XtsStorage(Storage baseStorage, Span<byte> key, int sectorSize, bool keepOpen)
            : base(baseStorage, sectorSize, keepOpen)
        {
            if (key == null) throw new NullReferenceException(nameof(key));
            if (key.Length != BlockSize * 2) throw new ArgumentException(nameof(key), $"Key must be {BlockSize * 2} bytes long");

            _tempBuffer = new byte[sectorSize];
            _tempBuffer2 = new byte[sectorSize];
            _key1 = key.Slice(0, BlockSize).ToArray();
            _key2 = key.Slice(BlockSize, BlockSize).ToArray();

            Length = baseStorage.Length;
        }

        public override long Length { get; }
        protected override int ReadSpan(Span<byte> destination, long offset)
        {
            int size = destination.Length;
            long sectorIndex = offset / SectorSize;

            if (_decryptor == null) _decryptor = new Aes128XtsTransform(_key1, _key2, true);

            base.ReadSpan(_tempBuffer.AsSpan(0, size), offset);

            _decryptor.TransformBlock(_tempBuffer, 0, size, _tempBuffer2, 0, (ulong)sectorIndex);
            _tempBuffer2.AsSpan(0, size).CopyTo(destination);

            return size;
        }

        protected override void WriteSpan(ReadOnlySpan<byte> source, long offset)
        {
            int size = source.Length;
            long sectorIndex = offset / SectorSize;

            if (_encryptor == null) _encryptor = new Aes128XtsTransform(_key1, _key2, false);

            source.CopyTo(_tempBuffer);
            _encryptor.TransformBlock(_tempBuffer, 0, size, _tempBuffer2, 0, (ulong)sectorIndex);

            base.WriteSpan(_tempBuffer2.AsSpan(0, size), offset);
        }

        public override void Flush()
        {
            BaseStorage.Flush();
        }
    }
}
