using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Kernel
{
    [StructLayout(LayoutKind.Explicit, Size = 0x100)]
    public struct KipHeader
    {
        public const uint Kip1Magic = 0x3150494B; // KIP1
        public const int NameSize = 12;
        public const int SegmentCount = 6;

        [FieldOffset(0x00)] public uint Magic;

        [FieldOffset(0x04)] private byte _name;

        [FieldOffset(0x10)] public ulong ProgramId;
        [FieldOffset(0x18)] public int Version;

        [FieldOffset(0x1C)] public byte Priority;
        [FieldOffset(0x1D)] public byte IdealCoreId;
        [FieldOffset(0x1F)] public Flag Flags;

        [FieldOffset(0x20)] public int TextMemoryOffset;
        [FieldOffset(0x24)] public int TextSize;
        [FieldOffset(0x28)] public int TextFileSize;

        [FieldOffset(0x2C)] public int AffinityMask;

        [FieldOffset(0x30)] public int RoMemoryOffset;
        [FieldOffset(0x34)] public int RoSize;
        [FieldOffset(0x38)] public int RoFileSize;

        [FieldOffset(0x3C)] public int StackSize;

        [FieldOffset(0x40)] public int DataMemoryOffset;
        [FieldOffset(0x44)] public int DataSize;
        [FieldOffset(0x48)] public int DataFileSize;

        [FieldOffset(0x50)] public int BssMemoryOffset;
        [FieldOffset(0x54)] public int BssSize;
        [FieldOffset(0x58)] public int BssFileSize;

        [FieldOffset(0x80)] private uint _capabilities;

        public Span<byte> Name => SpanHelpers.CreateSpan(ref _name, NameSize);

        public Span<SegmentHeader> Segments =>
            SpanHelpers.CreateSpan(ref Unsafe.As<int, SegmentHeader>(ref TextMemoryOffset), SegmentCount);

        public Span<uint> Capabilities => SpanHelpers.CreateSpan(ref _capabilities, 0x80 / sizeof(uint));

        public bool IsValid => Magic == Kip1Magic;

        [Flags]
        public enum Flag : byte
        {
            TextCompress = 1 << 0,
            RoCompress = 1 << 1,
            DataCompress = 1 << 2,
            Is64BitInstruction = 1 << 3,
            ProcessAddressSpace64Bit = 1 << 4,
            UseSecureMemory = 1 << 5
        }

        [StructLayout(LayoutKind.Sequential, Size = 0x10)]
        public struct SegmentHeader
        {
            public int MemoryOffset;
            public int Size;
            public int FileSize;
        }
    }
}
