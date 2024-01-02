using System;
using System.Buffers.Binary;
using LibHac.Diag;
using LibHac.Util;

namespace LibHac.FsSystem;

public static class BitmapUtils
{
    // ReSharper disable once InconsistentNaming
    public static int ILog2(uint value)
    {
        Assert.SdkRequiresGreater(value, 0u);

        const int intBitCount = 32;
        return intBitCount - 1 - BitUtil.CountLeadingZeros(value);
    }

    internal static uint ReadU32(ReadOnlySpan<byte> buffer, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(offset));
    }

    internal static void WriteU32(Span<byte> buffer, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(buffer.Slice(offset), value);
    }

    internal static int CountLeadingZeros(uint value)
    {
        return BitUtil.CountLeadingZeros(value);
    }

    internal static int CountLeadingOnes(uint value)
    {
        return CountLeadingZeros(~value);
    }
}