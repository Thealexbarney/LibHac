// ReSharper disable InconsistentNaming
using System.Runtime.CompilerServices;
using LibHac.Boot;
using Xunit;

namespace LibHac.Tests.Boot
{
    public class TypeSizeTests
    {
        [Fact]
        public static void EncryptedKeyBlobSizeIs0xB0()
        {
            Assert.Equal(0xB0, Unsafe.SizeOf<EncryptedKeyBlob>());
        }

        [Fact]
        public static void KeyBlobSizeIs0x90()
        {
            Assert.Equal(0x90, Unsafe.SizeOf<KeyBlob>());
        }
    }
}
