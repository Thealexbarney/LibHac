using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;

namespace LibHac.Loader;

public struct NsoHeader
{
    public static readonly int SegmentCount = 3;

    public uint Magic;
    public uint Version;
    public uint Reserved08;
    public Flag Flags;

    public uint TextFileOffset;
    public uint TextMemoryOffset;
    public uint TextSize;

    public uint ModuleNameOffset;

    public uint RoFileOffset;
    public uint RoMemoryOffset;
    public uint RoSize;

    public uint ModuleNameSize;

    public uint DataFileOffset;
    public uint DataMemoryOffset;
    public uint DataSize;

    public uint BssSize;

    public Array32<byte> ModuleId;

    // Size of the sections in the NSO file
    public uint TextFileSize;
    public uint RoFileSize;
    public uint DataFileSize;

    public Array28<byte> Reserved6C;

    public uint ApiInfoOffset;
    public uint ApiInfoSize;
    public uint DynStrOffset;
    public uint DynStrSize;
    public uint DynSymOffset;
    public uint DynSymSize;

    public Array32<byte> TextHash;
    public Array32<byte> RoHash;
    public Array32<byte> DataHash;

    [UnscopedRef]
    public Span<SegmentHeader> Segments =>
        SpanHelpers.CreateSpan(ref Unsafe.As<uint, SegmentHeader>(ref TextFileOffset), SegmentCount);

    [UnscopedRef] public Span<uint> CompressedSizes => SpanHelpers.CreateSpan(ref TextFileSize, SegmentCount);

    [UnscopedRef] public Span<Array32<byte>> SegmentHashes => SpanHelpers.CreateSpan(ref TextHash, SegmentCount);

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

    [StructLayout(LayoutKind.Sequential)]
    public struct SegmentHeader
    {
        public uint FileOffset;
        public uint MemoryOffset;
        public uint Size;
        private int _unused;
    }
}