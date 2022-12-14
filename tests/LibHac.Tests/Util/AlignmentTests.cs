using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Util
{
    public class AlignmentTests
    {
        [Theory]
        [InlineData(0, 0x40, 0)]
        [InlineData(0x3F, 0x40, 0x40)]
        [InlineData(0x40, 0x40, 0x40)]
        [InlineData(0x41, 0x40, 0x80)]
        public void AlignUp_Byte(byte value, uint alignment, byte expectedValue)
        {
            var actualValue = Alignment.AlignUp(value, alignment);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(0, 0x40, 0)]
        [InlineData(-0x3F, 0x40, 0)]
        [InlineData(-0x40, 0x40, -0x40)]
        [InlineData(-0x41, 0x40, -0x40)]
        [InlineData(-0x41, 0, 0)]
        [InlineData(int.MaxValue, 0x40, int.MinValue)]
        public void AlignUp_Int(int value, uint alignment, int expectedValue)
        {
            var actualValue = Alignment.AlignUp(value, alignment);
            Assert.Equal(expectedValue, actualValue);
        }
        
        [Theory]
        [InlineData(0, 0x40, 0)]
        [InlineData(0x3F, 0x40, 0x40)]
        [InlineData(0x40, 0x40, 0x40)]
        [InlineData(0x41, 0x40, 0x80)]
        [InlineData(0xFFF_FFFF_8000, 0x10000, 0x1000_0000_0000)]
        public void AlignUp_Ulong(ulong value, uint alignment, ulong expectedValue)
        {
            var actualValue = Alignment.AlignUp(value, alignment);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(0, 0x40, 0)]
        [InlineData(0x3F, 0x40, 0)]
        [InlineData(0x40, 0x40, 0x40)]
        [InlineData(0x41, 0x40, 0x40)]
        public void AlignDown_Byte(byte value, uint alignment, byte expectedValue)
        {
            var actualValue = Alignment.AlignDown(value, alignment);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(0, 0x40, 0)]
        [InlineData(0x3F, 0x40, 0)]
        [InlineData(0x40, 0x40, 0x40)]
        [InlineData(0x41, 0x40, 0x40)]
        public void AlignDown_Long(long value, uint alignment, long expectedValue)
        {
            var actualValue = Alignment.AlignDown(value, alignment);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(0, 0x40, true)]
        [InlineData(0x3F, 0x40, false)]
        [InlineData(0x40, 0x40, true)]
        [InlineData(0x41, 0x40, false)]
        public void IsAligned_Byte(byte value, uint alignment, bool expectedValue)
        {
            var actualValue = Alignment.IsAligned(value, alignment);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(0, 0x40, true)]
        [InlineData(0x3F, 0x40, false)]
        [InlineData(0x40, 0x40, true)]
        [InlineData(0x41, 0x40, false)]
        [InlineData(0xFFF_FFFF_8000, 0x400, true)]
        public void IsAligned_Long(long value, uint alignment, bool expectedValue)
        {
            var actualValue = Alignment.IsAligned(value, alignment);
            Assert.Equal(expectedValue, actualValue);
        }

        [Theory]
        [InlineData(0, 0)]
        [InlineData(0x3F, 1)]
        [InlineData(0x40, 0x40)]
        [InlineData(0x41, 1)]
        [InlineData(0x42, 2)]
        [InlineData(0xFF900000, 0x100000)]
        public void GetAlignment_Uint(uint value, long expectedValue)
        {
            var actualValue = Alignment.GetAlignment(value);
            Assert.Equal(expectedValue, actualValue);
        }
    }
}
