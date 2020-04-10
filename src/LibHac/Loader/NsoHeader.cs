using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Loader
{
    [StructLayout(LayoutKind.Explicit, Size = 0x100)]
    public struct NsoHeader
    {
        public const int SegmentCount = 3;

        [FieldOffset(0x00)] public uint Magic;
        [FieldOffset(0x04)] public uint Version;
        [FieldOffset(0x08)] public uint Reserved08;
        [FieldOffset(0x0C)] public Flag Flags;

        [FieldOffset(0x10)] public uint TextFileOffset;
        [FieldOffset(0x14)] public uint TextMemoryOffset;
        [FieldOffset(0x18)] public uint TextSize;

        [FieldOffset(0x1C)] public uint ModuleNameOffset;

        [FieldOffset(0x20)] public uint RoFileOffset;
        [FieldOffset(0x24)] public uint RoMemoryOffset;
        [FieldOffset(0x28)] public uint RoSize;

        [FieldOffset(0x2C)] public uint ModuleNameSize;

        [FieldOffset(0x30)] public uint DataFileOffset;
        [FieldOffset(0x34)] public uint DataMemoryOffset;
        [FieldOffset(0x38)] public uint DataSize;

        [FieldOffset(0x3C)] public uint BssSize;

        [FieldOffset(0x40)] public Buffer32 ModuleId;

        // Size of the sections in the NSO file
        [FieldOffset(0x60)] public uint TextFileSize;
        [FieldOffset(0x64)] public uint RoFileSize;
        [FieldOffset(0x68)] public uint DataFileSize;

        [FieldOffset(0x6C)] private byte _reserved6C;

        [FieldOffset(0x88)] public uint ApiInfoOffset;
        [FieldOffset(0x8C)] public uint ApiInfoSize;
        [FieldOffset(0x90)] public uint DynStrOffset;
        [FieldOffset(0x94)] public uint DynStrSize;
        [FieldOffset(0x98)] public uint DynSymOffset;
        [FieldOffset(0x9C)] public uint DynSymSize;

        [FieldOffset(0xA0)] public Buffer32 TextHash;
        [FieldOffset(0xC0)] public Buffer32 RoHash;
        [FieldOffset(0xE0)] public Buffer32 DataHash;

        public Span<SegmentHeader> Segments =>
            SpanHelpers.CreateSpan(ref Unsafe.As<uint, SegmentHeader>(ref TextFileOffset), SegmentCount);

        public Span<uint> CompressedSizes => SpanHelpers.CreateSpan(ref TextFileSize, SegmentCount);

        public Span<Buffer32> SegmentHashes => SpanHelpers.CreateSpan(ref TextHash, SegmentCount);

        public Span<byte> Reserved6C => SpanHelpers.CreateSpan(ref _reserved6C, 0x1C);

        [Flags]
        public enum Flag
        {
            TextCompress = 1 << 0,
            RoCompress = 1 << 1,
            DataCompress = 1 << 2,
            TextHash = 1 << 3,
            RoHash = 1 << 4,
            DataHash = 1 << 5
        }

        [StructLayout(LayoutKind.Sequential, Size = 0x10)]
        public struct SegmentHeader
        {
            public uint FileOffset;
            public uint MemoryOffset;
            public uint Size;
        }
    }
}
