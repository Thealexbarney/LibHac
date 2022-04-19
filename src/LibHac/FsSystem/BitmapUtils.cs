using LibHac.Diag;

namespace LibHac.FsSystem;

public static class BitmapUtils
{
    // ReSharper disable once InconsistentNaming
    public static int ILog2(uint value)
    {
        Assert.SdkRequiresGreater(value, 0u);

        const int intBitCount = 32;
        return intBitCount - 1 - Util.BitUtil.CountLeadingZeros(value);
    }
}