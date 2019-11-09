using LibHac.Crypto2;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class AesCtrTests
    {
        public static TheoryData<EncryptionTestVector> TestVectors = RspReader.ReadTestVectors(true, "CTR128.rsp");

        [Theory]
        [MemberData(nameof(TestVectors))]
        public static void Transform(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, AesCrypto.CreateCtrEncryptor(tv.Key, tv.Iv, true));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(TestVectors))]
        public static void TransformIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, AesCrypto.CreateCtrEncryptor(tv.Key, tv.Iv));
        }
    }
}
