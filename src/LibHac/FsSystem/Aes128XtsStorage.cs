using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class Aes128XtsStorage : SectorStorage
    {
        private const int BlockSize = 0x10;

        private readonly byte[] _tempBuffer;

        private Aes128XtsTransform _readTransform;
        private Aes128XtsTransform _writeTransform;

        private readonly byte[] _key1;
        private readonly byte[] _key2;

        private readonly bool _decryptRead;

        public Aes128XtsStorage(IStorage baseStorage, Span<byte> key, int sectorSize, bool leaveOpen, bool decryptRead = true)
            : base(baseStorage, sectorSize, leaveOpen)
        {
            if (key == null) throw new NullReferenceException(nameof(key));
            if (key.Length != BlockSize * 2) throw new ArgumentException(nameof(key), $"Key must be {BlockSize * 2} bytes long");

            _tempBuffer = new byte[sectorSize];
            _decryptRead = decryptRead;
            _key1 = key.Slice(0, BlockSize).ToArray();
            _key2 = key.Slice(BlockSize, BlockSize).ToArray();
        }

        public Aes128XtsStorage(IStorage baseStorage, Span<byte> key1, Span<byte> key2, int sectorSize, bool leaveOpen, bool decryptRead = true)
            : base(baseStorage, sectorSize, leaveOpen)
        {
            if (key1 == null) throw new NullReferenceException(nameof(key1));
            if (key2 == null) throw new NullReferenceException(nameof(key2));
            if (key1.Length != BlockSize || key1.Length != BlockSize) throw new ArgumentException($"Keys must be {BlockSize} bytes long");

            _tempBuffer = new byte[sectorSize];
            _decryptRead = decryptRead;
            _key1 = key1.ToArray();
            _key2 = key2.ToArray();
        }

        protected override Result DoRead(long offset, Span<byte> destination)
        {
            int size = destination.Length;
            long sectorIndex = offset / SectorSize;

            if (_readTransform == null) _readTransform = new Aes128XtsTransform(_key1, _key2, _decryptRead);

            Result rc = base.DoRead(offset, _tempBuffer.AsSpan(0, size));
            if (rc.IsFailure()) return rc;

            _readTransform.TransformBlock(_tempBuffer, 0, size, (ulong)sectorIndex);
            _tempBuffer.AsSpan(0, size).CopyTo(destination);

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
        {
            int size = source.Length;
            long sectorIndex = offset / SectorSize;

            if (_writeTransform == null) _writeTransform = new Aes128XtsTransform(_key1, _key2, !_decryptRead);

            source.CopyTo(_tempBuffer);
            _writeTransform.TransformBlock(_tempBuffer, 0, size, (ulong)sectorIndex);

            return base.DoWrite(offset, _tempBuffer.AsSpan(0, size));
        }

        protected override Result DoFlush()
        {
            return BaseStorage.Flush();
        }
    }
}
