using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class AesCtrTests
    {
        public static TheoryData<EncryptionTestVector> TestVectors = RspReader.ReadEncryptionTestVectors(true, "CTR128.rsp");

        [Theory]
        [MemberData(nameof(TestVectors))]
        public static void Transform(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCtrEncryptor(tv.Key, tv.Iv, true));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(TestVectors))]
        public static void TransformIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCtrEncryptor(tv.Key, tv.Iv));
        }
    }
}
