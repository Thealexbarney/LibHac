using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using LibHac.Common.FixedArrays;

namespace LibHac.Fs.Impl;

public struct InitialDataAad
{
    public Array32<byte> Value;
}

public struct KeySeed
{
    public Array16<byte> Value;
}

public struct InitialDataMac
{
    private Array2<ulong> _value;

    [UnscopedRef]
    public Span<byte> Value => MemoryMarshal.Cast<ulong, byte>(_value);
}

public struct ExportReportInfo
{
    public byte DiffChunkCount;
    public byte DoubleDivisionDiffChunkCount;
    public byte HalfDivisionDiffChunkCount;
    public byte CompressionRate;
    public Array28<byte> Reserved;
}

public struct ImportReportInfo
{
    public byte DiffChunkCount;
    public byte DoubleDivisionDiffChunkCount;
    public byte HalfDivisionDiffChunkCount;
    public byte CompressionRate;
    public Array28<byte> Reserved;
}