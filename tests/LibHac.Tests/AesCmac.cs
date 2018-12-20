using Xunit;

namespace LibHac.Tests
{
    public class AesCmac
    {
        private static readonly byte[] CmacKey = "2b7e151628aed2a6abf7158809cf4f3c".ToBytes();

        private static readonly TestData[] TestVectors =
        {
            new TestData
            {
                Key = CmacKey,
                Message = "".ToBytes(),
                Expected = "bb1d6929e95937287fa37d129b756746".ToBytes()
            },
            new TestData
            {
                Key = CmacKey,
                Message = "6bc1bee22e409f96e93d7e117393172a".ToBytes(),
                Expected = "070a16b46b4d4144f79bdd9dd04a287c".ToBytes()
            },
            new TestData
            {
                Key = CmacKey,
                Message = "6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e5130c81c46a35ce411".ToBytes(),
                Expected = "dfa66747de9ae63030ca32611497c827".ToBytes()
            },
            new TestData
            {
                Key = CmacKey,
                Message = "6bc1bee22e409f96e93d7e117393172aae2d8a571e03ac9c9eb76fac45af8e5130c81c46a35ce411e5fbc1191a0a52eff69f2445df4f9b17ad2b417be66c3710".ToBytes(),
                Expected = "51f0bebf7e3b9d92fc49741779363cfe".ToBytes()
            }
        };

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public static void Encrypt(int index)
        {
            TestData data = TestVectors[index];
            var actual = new byte[0x10];

            Crypto.CalculateAesCmac(data.Key, data.Message, 0, actual, 0, data.Message.Length);

            Assert.Equal(data.Expected, actual);
        }

        private struct TestData
        {
            public byte[] Key;
            public byte[] Message;
            public byte[] Expected;
        }
    }
}
