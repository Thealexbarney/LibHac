// ReSharper disable InconsistentNaming
using System.Runtime.CompilerServices;
using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class TypeSizeTests
    {
        [Fact]
        public static void AesKeySizeIs0x10()
        {
            Assert.Equal(0x10, Unsafe.SizeOf<AesKey>());
        }

        [Fact]
        public static void AesXtsKeySizeIs0x20()
        {
            Assert.Equal(0x20, Unsafe.SizeOf<AesXtsKey>());
        }

        [Fact]
        public static void AesIvSizeIs0x10()
        {
            Assert.Equal(0x10, Unsafe.SizeOf<AesIv>());
        }

        [Fact]
        public static void AesCmacSizeIs0x10()
        {
            Assert.Equal(0x10, Unsafe.SizeOf<Crypto.AesCmac>());
        }
    }
}
