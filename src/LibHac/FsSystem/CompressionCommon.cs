using System;
using LibHac.Common;

namespace LibHac.FsSystem;

public enum CompressionType : byte
{
    None = 0,
    FillZero = 1,
    Lz4 = 3,
    Unknown = 4
}

public delegate Result CompressionGetData(Span<byte> buffer);
public delegate Result CompressionProcessData(Span<byte> workBuffer, int sizeBufferRequired, CompressionGetData getDataFunc);

public static class CompressionTypeUtility
{
    public static bool IsBlockAlignmentRequired(CompressionType type)
    {
        return type != CompressionType.None && type != CompressionType.FillZero;
    }

    public static bool IsDataStorageAccessRequired(CompressionType type)
    {
        return type != CompressionType.FillZero;
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

public interface IDecompressorFactory
{
    public Result GetDecompressor(ref UniqueRef<IDecompressor> outDecompressor, CompressionProcessData processDataFunc, Span<byte> buffer);
}

public interface IDecompressor : IDisposable
{
    Result Decompress(CompressionType type, int compressedSize, Span<byte> buffer, int decompressedSize);
    Result Unk();
}