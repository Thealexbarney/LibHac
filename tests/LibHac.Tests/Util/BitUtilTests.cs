using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Util;

public class BitUtilTests
{
    [Theory]
    [InlineData(0x80, 0)]
    [InlineData(0x00, 8)]
    public void CountLeadingZeros_Byte(byte value, int expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.CountLeadingZeros(value));
    }

    [Theory]
    [InlineData(0x0080, 8)]
    [InlineData(0x0000, 16)]
    public void CountLeadingZeros_Short(short value, int expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.CountLeadingZeros(value));
    }

    [Theory]
    [InlineData(0b0000_1000_0110_0000, 0b0000_1000_0100_0000)]
    [InlineData(0b0000_1000_0110_1111, 0b0000_1000_0110_1110)]
    [InlineData(unchecked((short)0b1000_0000_0000_0000), 0b0000_0000_0000_0000)]
    [InlineData(0, 0)]
    public void ResetLeastSignificantOneBit_Short(short value, short expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.ResetLeastSignificantOneBit(value));
    }

    [Theory]
    [InlineData(0b0101_0000_0000_0000_0000, 0b0100_0000_0000_0000_0000)]
    [InlineData(0b1111_0000_1000_0110_1111, 0b1111_0000_1000_0110_1110)]
    [InlineData(0b0100_1000_0000_0000_0000, 0b0100_0000_0000_0000_0000)]
    [InlineData(0, 0)]
    public void ResetLeastSignificantOneBit_Uint(uint value, uint expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.ResetLeastSignificantOneBit(value));
    }

    [Theory]
    [InlineData(0x80, true)]
    [InlineData(0x81, false)]
    [InlineData(0, false)]
    [InlineData(short.MinValue, false)]
    public void IsPowerOfTwo_Short(short value, bool expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.IsPowerOfTwo(value));
    }

    [Theory]
    [InlineData(0x0000100000000000, true)]
    [InlineData(0x0000100000004000, false)]
    [InlineData(0, false)]
    public void IsPowerOfTwo_Ulong(ulong value, bool expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.IsPowerOfTwo(value));
    }

    [Theory]
    [InlineData(-55, -2, 29)]
    [InlineData(-55, 2, -27)]
    [InlineData(0, 26, 0)]
    [InlineData(-55, -26, 3)]
    [InlineData(55, -26, -1)]
    [InlineData(int.MinValue, -26, -82595523)]
    public void DivideUp_Int(int value, int divisor, int expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.DivideUp(value, divisor));
    }

    [Theory]
    [InlineData(55, 2, 28)]
    [InlineData(0, 26, 0)]
    [InlineData(1127, 24, 47)]
    [InlineData(1128, 24, 47)]
    [InlineData(1129, 24, 48)]
    [InlineData(567, 987, 1)]
    [InlineData(int.MaxValue, 26, 82595525)]
    public void DivideUp_Uint(uint value, uint divisor, uint expectedValue)
    {
        Assert.Equal(expectedValue, BitUtil.DivideUp(value, divisor));
    }
}