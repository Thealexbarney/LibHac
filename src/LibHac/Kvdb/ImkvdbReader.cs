using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using static LibHac.Results;
using static LibHac.Kvdb.ResultsKvdb;

namespace LibHac.Kvdb
{
    public ref struct ImkvdbReader
    {
        private ReadOnlySpan<byte> Data;
        private int _position;

        public ImkvdbReader(ReadOnlySpan<byte> data)
        {
            Data = data;
            _position = 0;
        }

        public Result ReadHeader(out int entryCount)
        {
            entryCount = default;

            if (_position + Unsafe.SizeOf<ImkvdbHeader>() > Data.Length) return ResultKvdbInvalidKeyValue;

            ref ImkvdbHeader header = ref Unsafe.As<byte, ImkvdbHeader>(ref Unsafe.AsRef(Data[_position]));

            if (header.Magic != ImkvdbHeader.ExpectedMagic)
            {
                return ResultKvdbInvalidKeyValue;
            }

            entryCount = header.EntryCount;
            _position += Unsafe.SizeOf<ImkvdbHeader>();

            return ResultSuccess;
        }

        public Result GetEntrySize(out int keySize, out int valueSize)
        {
            keySize = default;
            valueSize = default;

            if (_position + Unsafe.SizeOf<ImkvdbHeader>() > Data.Length) return ResultKvdbInvalidKeyValue;

            ref ImkvdbEntryHeader header = ref Unsafe.As<byte, ImkvdbEntryHeader>(ref Unsafe.AsRef(Data[_position]));

            if (header.Magic != ImkvdbEntryHeader.ExpectedMagic)
            {
                return ResultKvdbInvalidKeyValue;
            }

            keySize = header.KeySize;
            valueSize = header.ValueSize;

            return ResultSuccess;
        }

        public Result ReadEntry(out ReadOnlySpan<byte> key, out ReadOnlySpan<byte> value)
        {
            key = default;
            value = default;

            Result sizeResult = GetEntrySize(out int keySize, out int valueSize);
            if (sizeResult.IsFailure()) return sizeResult;

            _position += Unsafe.SizeOf<ImkvdbEntryHeader>();

            if (_position + keySize + valueSize > Data.Length) return ResultKvdbInvalidKeyValue;

            key = Data.Slice(_position, keySize);
            value = Data.Slice(_position + keySize, valueSize);

            _position += keySize + valueSize;

            return ResultSuccess;
        }
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    internal struct ImkvdbHeader
    {
        public const uint ExpectedMagic = 0x564B4D49; // IMKV

        public uint Magic;
        public int Reserved;
        public int EntryCount;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0xC)]
    internal struct ImkvdbEntryHeader
    {
        public const uint ExpectedMagic = 0x4E454D49; // IMEN

        public uint Magic;
        public int KeySize;
        public int ValueSize;
    }
}
