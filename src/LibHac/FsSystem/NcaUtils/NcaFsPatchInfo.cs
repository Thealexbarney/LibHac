using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace LibHac.FsSystem.NcaUtils
{
    public struct NcaFsPatchInfo
    {
        private readonly Memory<byte> _data;

        public NcaFsPatchInfo(Memory<byte> data)
        {
            _data = data;
        }

        private ref PatchInfoStruct Data => ref Unsafe.As<byte, PatchInfoStruct>(ref _data.Span[0]);

        public long RelocationTreeOffset
        {
            get => Data.RelocationTreeOffset;
            set => Data.RelocationTreeOffset = value;
        }

        public long RelocationTreeSize
        {
            get => Data.RelocationTreeSize;
            set => Data.RelocationTreeSize = value;
        }

        public long EncryptionTreeOffset
        {
            get => Data.EncryptionTreeOffset;
            set => Data.EncryptionTreeOffset = value;
        }

        public long EncryptionTreeSize
        {
            get => Data.EncryptionTreeSize;
            set => Data.EncryptionTreeSize = value;
        }

        public Span<byte> RelocationTreeHeader => _data.Span.Slice(0x10, 0x10);
        public Span<byte> EncryptionTreeHeader => _data.Span.Slice(0x30, 0x10);

        [StructLayout(LayoutKind.Explicit)]
        private struct PatchInfoStruct
        {
            [FieldOffset(0x00)] public long RelocationTreeOffset;
            [FieldOffset(0x08)] public long RelocationTreeSize;
            [FieldOffset(0x20)] public long EncryptionTreeOffset;
            [FieldOffset(0x28)] public long EncryptionTreeSize;
        }
    }
}
