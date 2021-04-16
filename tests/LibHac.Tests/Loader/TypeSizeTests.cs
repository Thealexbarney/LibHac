using System.Runtime.CompilerServices;
using LibHac.Loader;
using Xunit;

namespace LibHac.Tests.Loader
{
    public class TypeSizeTests
    {
        [Fact]
        public static void MetaSizeIsCorrect()
        {
            Assert.Equal(0x80, Unsafe.SizeOf<Meta>());
        }

        [Fact]
        public static void AciSizeIsCorrect()
        {
            Assert.Equal(0x40, Unsafe.SizeOf<AciHeader>());
        }

        [Fact]
        public static void AcidSizeIsCorrect()
        {
            Assert.Equal(0x240, Unsafe.SizeOf<AcidHeaderData>());
        }
    }
}
