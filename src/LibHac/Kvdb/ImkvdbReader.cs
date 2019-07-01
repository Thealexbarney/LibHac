using System;
using System.Runtime.CompilerServices;

namespace LibHac.Kvdb
{
    public ref struct ImkvdbReader
    {
        private ReadOnlySpan<byte> _data;
        private int _position;

        public ImkvdbReader(ReadOnlySpan<byte> data)
        {
            _data = data;
            _position = 0;
        }

        public Result ReadHeader(out int entryCount)
        {
            entryCount = default;

            if (_position + Unsafe.SizeOf<ImkvdbHeader>() > _data.Length) return ResultKvdb.InvalidKeyValue;

            ref ImkvdbHeader header = ref Unsafe.As<byte, ImkvdbHeader>(ref Unsafe.AsRef(_data[_position]));

            if (header.Magic != ImkvdbHeader.ExpectedMagic)
            {
                return ResultKvdb.InvalidKeyValue;
            }

            entryCount = header.EntryCount;
            _position += Unsafe.SizeOf<ImkvdbHeader>();

            return Result.Success;
        }

        public Result GetEntrySize(out int keySize, out int valueSize)
        {
            keySize = default;
            valueSize = default;

            if (_position + Unsafe.SizeOf<ImkvdbHeader>() > _data.Length) return ResultKvdb.InvalidKeyValue;

            ref ImkvdbEntryHeader header = ref Unsafe.As<byte, ImkvdbEntryHeader>(ref Unsafe.AsRef(_data[_position]));

            if (header.Magic != ImkvdbEntryHeader.ExpectedMagic)
            {
                return ResultKvdb.InvalidKeyValue;
            }

            keySize = header.KeySize;
            valueSize = header.ValueSize;

            return Result.Success;
        }

        public Result ReadEntry(out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
        {
            key = default;
            value = default;

            Result sizeResult = GetEntrySize(out int keySize, out int valueSize);
            if (sizeResult.IsFailure()) return sizeResult;

            _position += Unsafe.SizeOf<ImkvdbEntryHeader>();

            if (_position + keySize + valueSize > _data.Length) return ResultKvdb.InvalidKeyValue;

            key = _data.Slice(_position, keySize);
            value = _data.Slice(_position + keySize, valueSize);

            _position += keySize + valueSize;

            return Result.Success;
        }
    }
}
