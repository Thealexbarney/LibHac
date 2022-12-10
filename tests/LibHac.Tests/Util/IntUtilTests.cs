using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Util;

public class IntUtilTests
{
    [Theory]
    [InlineData(0x80, 0x7F, true)]
    [InlineData(0x80, 0x80, false)]
    public void CanAddWithoutOverflow_Byte(byte x, byte y, bool expectedResult)
    {
        Assert.Equal(expectedResult, IntUtil.CanAddWithoutOverflow(x, y));
        Assert.Equal(expectedResult, IntUtil.CanAddWithoutOverflow(y, x));
    }

    [Theory]
    [InlineData(-0x8000, -1, false)]
    [InlineData(-0x8000, 0, true)]
    [InlineData(0x4000, 0x3FFF, true)]
    [InlineData(0x4000, 0x4000, false)]
    [InlineData(-0x4000, -0x4000, true)]
    public void CanAddWithoutOverflow_Short(short x, short y, bool expectedResult)
    {
        Assert.Equal(expectedResult, IntUtil.CanAddWithoutOverflow(x, y));
        Assert.Equal(expectedResult, IntUtil.CanAddWithoutOverflow(y, x));
    }

    [Theory]
    [InlineData(0x80, 0x80, true)]
    [InlineData(0x80, 0x81, false)]
    public void CanSubtractWithoutOverflow_Byte(byte x, byte y, bool expectedResult)
    {
        Assert.Equal(expectedResult, IntUtil.CanSubtractWithoutOverflow(x, y));
    }

    [Theory]
    [InlineData(-0x8000, 1, false)]
    [InlineData(-0x8000, 0, true)]
    [InlineData(-0x4000, 0x4000, true)]
    [InlineData(-0x4000, 0x4001, false)]
    [InlineData(0x4000, -0x4000, false)]
    public void CanSubtractWithoutOverflow_Short(short x, short y, bool expectedResult)
    {
        Assert.Equal(expectedResult, IntUtil.CanSubtractWithoutOverflow(x, y));
    }

    [Theory]
    [InlineData(0xFF, 0, true)]
    [InlineData(0x55, 3, true)]
    [InlineData(0x40, 4, false)]
    public void CanMultiplyWithoutOverflow_Byte(byte x, byte y, bool expectedResult)
    {
        Assert.Equal(expectedResult, IntUtil.CanMultiplyWithoutOverflow(x, y));
        Assert.Equal(expectedResult, IntUtil.CanMultiplyWithoutOverflow(y, x));
    }

    [Theory]
    [InlineData(0x7FFF, 0, true)]
    [InlineData(+0x1249, +7, true)]
    [InlineData(-0x1249, +7, true)]
    [InlineData(-0x1249, -7, true)]
    [InlineData(+0x1249, -7, true)]
    [InlineData(+0x2000, +4, false)]
    [InlineData(-0x2000, +4, true)]
    [InlineData(-0x2000, -4, false)]
    [InlineData(+0x2000, -4, true)]
    [InlineData(-0x2001, +4, false)]
    [InlineData(+0x2001, -4, false)]
    [InlineData(-0x8000, -1, false)]
    [InlineData(-0x7FFF, -1, true)]
    public void CanMultiplyWithoutOverflow_Short(short x, short y, bool expectedResult)
    {
        Assert.Equal(expectedResult, IntUtil.CanMultiplyWithoutOverflow(x, y));
        Assert.Equal(expectedResult, IntUtil.CanMultiplyWithoutOverflow(y, x));
    }

    [Theory]
    [InlineData(0x5555555555555555, 3, true)]
    [InlineData(0x4000000000000000, 4, false)]
    public void CanMultiplyWithoutOverflow_Ulong(ulong x, ulong y, bool expectedResult)
    {
        Assert.Equal(expectedResult, IntUtil.CanMultiplyWithoutOverflow(x, y));
        Assert.Equal(expectedResult, IntUtil.CanMultiplyWithoutOverflow(y, x));
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(unchecked((long)0x8111111100000000), false)]
    [InlineData(long.MinValue, false)]
    [InlineData(long.MaxValue, true)]
    public void IsIntValueRepresentable_LongToUlong(long value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<ulong, long>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(5, true)]
    [InlineData(long.MaxValue, true)]
    [InlineData(long.MaxValue + 1LU, false)]
    [InlineData(ulong.MaxValue, false)]
    public void IsIntValueRepresentable_ULongToLong(ulong value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<long, ulong>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(int.MinValue, true)]
    [InlineData(int.MaxValue, true)]
    [InlineData(int.MinValue - 1L, false)]
    [InlineData(int.MaxValue + 1L, false)]
    public void IsIntValueRepresentable_LongToInt(long value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<int, long>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, true)]
    [InlineData(short.MinValue, true)]
    [InlineData(short.MaxValue, true)]
    [InlineData(short.MinValue - 1L, false)]
    [InlineData(short.MaxValue + 1L, false)]
    public void IsIntValueRepresentable_LongToShort(long value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<short, long>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(uint.MaxValue, true)]
    [InlineData(uint.MaxValue + 1L, false)]
    public void IsIntValueRepresentable_LongToUint(long value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<uint, long>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(ushort.MaxValue, true)]
    [InlineData(ushort.MaxValue + 1L, false)]
    public void IsIntValueRepresentable_LongToUshort(long value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<ushort, long>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(-1, false)]
    [InlineData(short.MinValue, false)]
    [InlineData(short.MaxValue, true)]
    public void IsIntValueRepresentable_ShortToUshort(short value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<ushort, short>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(0xFFFFF000, false)]
    [InlineData(0xFFFF7FFF, false)]
    [InlineData(short.MaxValue, true)]
    [InlineData(short.MaxValue + 1, false)]
    public void IsIntValueRepresentable_UintToShort(uint value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<short, uint>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(uint.MaxValue, true)]
    [InlineData((ulong)uint.MaxValue + 1, false)]
    public void IsIntValueRepresentable_UlongToUint(ulong value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<uint, ulong>(value);
        Assert.Equal(expectedResult, actualResult);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(uint.MaxValue, true)]
    public void IsIntValueRepresentable_UintToUlong(uint value, bool expectedResult)
    {
        bool actualResult = IntUtil.IsIntValueRepresentable<ulong, uint> (value);
        Assert.Equal(expectedResult, actualResult);
    }
}