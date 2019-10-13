using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.FsSystem.NcaUtils
{
    public struct NcaFsIntegrityInfoSha256
    {
        private readonly Memory<byte> _data;

        public NcaFsIntegrityInfoSha256(Memory<byte> data)
        {
            _data = data;
        }

        private ref Sha256Struct Data => ref Unsafe.As<byte, Sha256Struct>(ref _data.Span[0]);

        private ref Sha256Level GetLevelInfo(int index)
        {
            ValidateLevelIndex(index);

            int offset = Sha256Struct.Sha256LevelOffset + Sha256Level.Sha256LevelSize * index;
            return ref Unsafe.As<byte, Sha256Level>(ref _data.Span[offset]);
        }

        public int BlockSize
        {
            get => Data.BlockSize;
            set => Data.BlockSize = value;
        }

        public int LevelCount
        {
            get => Data.LevelCount;
            set => Data.LevelCount = value;
        }

        public Span<byte> MasterHash => _data.Span.Slice(Sha256Struct.MasterHashOffset, Sha256Struct.MasterHashSize);

        public ref long GetLevelOffset(int index) => ref GetLevelInfo(index).Offset;
        public ref long GetLevelSize(int index) => ref GetLevelInfo(index).Size;

        private static void ValidateLevelIndex(int index)
        {
            if (index < 0 || index > 5)
            {
                throw new ArgumentOutOfRangeException($"IVFC level index must be between 0 and 5. Actual: {index}");
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Sha256Struct
        {
            public const int MasterHashOffset = 0;
            public const int MasterHashSize = 0x20;
            public const int Sha256LevelOffset = 0x28;

            [FieldOffset(0x20)] public int BlockSize;
            [FieldOffset(0x24)] public int LevelCount;
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct Sha256Level
        {
            public const int Sha256LevelSize = 0x10;

            [FieldOffset(0)] public long Offset;
            [FieldOffset(8)] public long Size;
        }
    }
}
