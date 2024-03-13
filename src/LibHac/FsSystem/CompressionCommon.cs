using System;

namespace LibHac.FsSystem;

public enum CompressionType : byte
{
    None = 0,
    Zeroed = 1,
    Lz4 = 3,
    Unknown = 4
}

public ref struct DecompressionTask
{
    public Span<byte> Destination;
    public ReadOnlySpan<byte> Source;
}

public delegate Result DecompressorFunction(DecompressionTask task);

public delegate DecompressorFunction GetDecompressorFunction(CompressionType compressionType);

public static class CompressionTypeUtility
{
    public static bool IsBlockAlignmentRequired(CompressionType type)
    {
        return type != CompressionType.None && type != CompressionType.Zeroed;
    }

    public static bool IsDataStorageAccessRequired(CompressionType type)
    {
        return type != CompressionType.Zeroed;
    }

    public static bool IsRandomAccessible(CompressionType type)
    {
        return type == CompressionType.None;
    }

    public static bool IsUnknownType(CompressionType type)
    {
        return type >= CompressionType.Unknown;
    }
}