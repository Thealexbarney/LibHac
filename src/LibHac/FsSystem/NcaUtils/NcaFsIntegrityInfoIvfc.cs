using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.FsSystem.NcaUtils
{
    public struct NcaFsIntegrityInfoIvfc
    {
        private readonly Memory<byte> _data;

        public NcaFsIntegrityInfoIvfc(Memory<byte> data)
        {
            _data = data;
        }

        private ref IvfcStruct Data => ref Unsafe.As<byte, IvfcStruct>(ref _data.Span[0]);

        private ref IvfcLevel GetLevelInfo(int index)
        {
            ValidateLevelIndex(index);

            int offset = IvfcStruct.IvfcLevelsOffset + IvfcLevel.IvfcLevelSize * index;
            return ref Unsafe.As<byte, IvfcLevel>(ref _data.Span[offset]);
        }

        public uint Magic
        {
            get => Data.Magic;
            set => Data.Magic = value;
        }

        public int Version
        {
            get => Data.Version;
            set => Data.Version = value;
        }

        public int MasterHashSize
        {
            get => Data.MasterHashSize;
            set => Data.MasterHashSize = value;
        }

        public int LevelCount
        {
            get => Data.LevelCount;
            set => Data.LevelCount = value;
        }

        public Span<byte> SaltSource => _data.Span.Slice(IvfcStruct.SaltSourceOffset, IvfcStruct.SaltSourceSize);
        public Span<byte> MasterHash => _data.Span.Slice(IvfcStruct.MasterHashOffset, MasterHashSize);

        public ref long GetLevelOffset(int index) => ref GetLevelInfo(index).Offset;
        public ref long GetLevelSize(int index) => ref GetLevelInfo(index).Size;
        public ref int GetLevelBlockSize(int index) => ref GetLevelInfo(index).BlockSize;

        private static void ValidateLevelIndex(int index)
        {
            if (index < 0 || index > 6)
            {
                throw new ArgumentOutOfRangeException($"IVFC level index must be between 0 and 6. Actual: {index}");
            }
        }

        [StructLayout(LayoutKind.Explicit)]
        private struct IvfcStruct
        {
            public const int IvfcLevelsOffset = 0x10;
            public const int SaltSourceOffset = 0xA0;
            public const int SaltSourceSize = 0x20;
            public const int MasterHashOffset = 0xC0;

            [FieldOffset(0)] public uint Magic;
            [FieldOffset(4)] public int Version;
            [FieldOffset(8)] public int MasterHashSize;
            [FieldOffset(12)] public int LevelCount;
        }

        [StructLayout(LayoutKind.Explicit, Size = IvfcLevelSize)]
        private struct IvfcLevel
        {
            public const int IvfcLevelSize = 0x18;

            [FieldOffset(0)] public long Offset;
            [FieldOffset(8)] public long Size;
            [FieldOffset(0x10)] public int BlockSize;
        }
    }
}
