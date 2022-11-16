using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;

namespace LibHac.Kernel;

[StructLayout(LayoutKind.Sequential)]
public struct KipHeader
{
    public static readonly uint Kip1Magic = 0x3150494B; // KIP1
    public static readonly int SegmentCount = 6;

    public uint Magic;

    public Array12<byte> Name;

    public ulong ProgramId;
    public int Version;

    public byte Priority;
    public byte IdealCoreId;
    private byte _reserved1E;
    public Flag Flags;

    public int TextMemoryOffset;
    public int TextSize;
    public int TextFileSize;

    public int AffinityMask;

    public int RoMemoryOffset;
    public int RoSize;
    public int RoFileSize;

    public int StackSize;

    public int DataMemoryOffset;
    public int DataSize;
    public int DataFileSize;
    private byte _reserved4C;

    public int BssMemoryOffset;
    public int BssSize;
    public int BssFileSize;
    private byte _reserved5C;

    private Array2<SegmentHeader> _unusedSegmentHeaders;

    public Array32<uint> Capabilities;

    [UnscopedRef]
    public Span<SegmentHeader> Segments =>
         SpanHelpers.CreateSpan(ref Unsafe.As<int, SegmentHeader>(ref TextMemoryOffset), SegmentCount);

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

    [StructLayout(LayoutKind.Sequential)]
    public struct SegmentHeader
    {
        public int MemoryOffset;
        public int Size;
        public int FileSize;
        private int _unused;
    }
}