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
    public Array16<byte> Value;
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