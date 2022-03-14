using LibHac.Diag;

namespace LibHac.FsSystem;

public static class BitmapUtils
{
    // ReSharper disable once InconsistentNaming
    public static uint ILog2(uint value)
    {
        Assert.SdkRequiresGreater(value, 0u);

        const uint intBitCount = 32;
        return intBitCount - 1 - (uint)Util.BitUtil.CountLeadingZeros(value);
    }
}