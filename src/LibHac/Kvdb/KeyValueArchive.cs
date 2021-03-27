using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.Kvdb
{
    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    public struct KeyValueArchiveHeader
    {
        public const uint ExpectedMagic = 0x564B4D49; // IMKV

        public uint Magic;
        public int Reserved;
        public int EntryCount;

        public bool IsValid() => Magic == ExpectedMagic;

        public KeyValueArchiveHeader(int entryCount)
        {
            Magic = ExpectedMagic;
            Reserved = 0;
            EntryCount = entryCount;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    internal struct KeyValueArchiveEntryHeader
    {
        public const uint ExpectedMagic = 0x4E454D49; // IMEN

        public uint Magic;
        public int KeySize;
        public int ValueSize;

        public bool IsValid() => Magic == ExpectedMagic;

        public KeyValueArchiveEntryHeader(int keySize, int valueSize)
        {
            Magic = ExpectedMagic;
            KeySize = keySize;
            ValueSize = valueSize;
        }
    }

    internal struct KeyValueArchiveSizeCalculator
    {
        public long Size { get; private set; }

        public void Initialize()
        {
            Size = Unsafe.SizeOf<KeyValueArchiveHeader>();
        }

        public void AddEntry(int keySize, int valueSize)
        {
            Size += Unsafe.SizeOf<KeyValueArchiveEntryHeader>() + keySize + valueSize;
        }
    }

    internal ref struct KeyValueArchiveBufferReader
    {
        private ReadOnlySpan<byte> _buffer;
        private int _offset;

        public KeyValueArchiveBufferReader(ReadOnlySpan<byte> buffer)
        {
            _buffer = buffer;
            _offset = 0;
        }

        public Result ReadEntryCount(out int count)
        {
            UnsafeHelpers.SkipParamInit(out count);

            // This should only be called at the start of reading stream.
            Assert.SdkRequiresEqual(_offset, 0);

            // Read and validate header.
            var header = new KeyValueArchiveHeader();

            Result rc = Read(SpanHelpers.AsByteSpan(ref header));
            if (rc.IsFailure()) return rc;

            if (!header.IsValid())
                return ResultKvdb.InvalidKeyValue.Log();

            count = header.EntryCount;
            return Result.Success;
        }

        public Result GetKeyValueSize(out int keySize, out int valueSize)
        {
            UnsafeHelpers.SkipParamInit(out keySize, out valueSize);

            // This should only be called after ReadEntryCount.
            Assert.SdkNotEqual(_offset, 0);

            // Peek the next entry header.
            Unsafe.SkipInit(out KeyValueArchiveEntryHeader header);

            Result rc = Peek(SpanHelpers.AsByteSpan(ref header));
            if (rc.IsFailure()) return rc;

            if (!header.IsValid())
                return ResultKvdb.InvalidKeyValue.Log();

            keySize = header.KeySize;
            valueSize = header.ValueSize;

            return Result.Success;
        }

        public Result ReadKeyValue(Span<byte> keyBuffer, Span<byte> valueBuffer)
        {
            // This should only be called after ReadEntryCount.
            Assert.SdkNotEqual(_offset, 0);

            // Read the next entry header.
            Unsafe.SkipInit(out KeyValueArchiveEntryHeader header);

            Result rc = Read(SpanHelpers.AsByteSpan(ref header));
            if (rc.IsFailure()) return rc;

            if (!header.IsValid())
                return ResultKvdb.InvalidKeyValue.Log();

            // Key size and Value size must be correct.
            Assert.SdkEqual(keyBuffer.Length, header.KeySize);
            Assert.SdkEqual(valueBuffer.Length, header.ValueSize);

            rc = Read(keyBuffer);
            if (rc.IsFailure()) return rc;

            rc = Read(valueBuffer);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result Peek(Span<byte> destBuffer)
        {
            // Bounds check.
            if (_offset + destBuffer.Length > _buffer.Length ||
                _offset + destBuffer.Length <= _offset)
            {
                return ResultKvdb.InvalidKeyValue.Log();
            }

            _buffer.Slice(_offset, destBuffer.Length).CopyTo(destBuffer);
            return Result.Success;
        }

        private Result Read(Span<byte> destBuffer)
        {
            Result rc = Peek(destBuffer);
            if (rc.IsFailure()) return rc;

            _offset += destBuffer.Length;
            return Result.Success;
        }
    }

    internal ref struct KeyValueArchiveBufferWriter
    {
        private Span<byte> _buffer;
        private int _offset;

        public KeyValueArchiveBufferWriter(Span<byte> buffer)
        {
            _buffer = buffer;
            _offset = 0;
        }

        private void Write(ReadOnlySpan<byte> source)
        {
            // Bounds check.
            Abort.DoAbortUnless(_offset + source.Length <= _buffer.Length &&
                        _offset + source.Length > _offset);

            source.CopyTo(_buffer.Slice(_offset));
            _offset += source.Length;
        }

        public void WriteHeader(int entryCount)
        {
            // This should only be called at start of write.
            Assert.SdkEqual(_offset, 0);

            var header = new KeyValueArchiveHeader(entryCount);
            Write(SpanHelpers.AsByteSpan(ref header));
        }

        public void WriteEntry(ReadOnlySpan<byte> key, ReadOnlySpan<byte> value)
        {
            // This should only be called after writing header.
            Assert.SdkNotEqual(_offset, 0);

            var header = new KeyValueArchiveEntryHeader(key.Length, value.Length);
            Write(SpanHelpers.AsByteSpan(ref header));
            Write(key);
            Write(value);
        }
    }
}
