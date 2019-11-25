using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class AesEcbTests
    {
        public static TheoryData<EncryptionTestVector> EncryptTestVectors =
            RspReader.ReadEncryptionTestVectors(true, "ECBVarKey128.rsp", "ECBVarTxt128.rsp", "ECBKeySbox128.rsp", "ECBGFSbox128.rsp");

        public static TheoryData<EncryptionTestVector> DecryptTestVectors =
            RspReader.ReadEncryptionTestVectors(false, "ECBVarKey128.rsp", "ECBVarTxt128.rsp", "ECBKeySbox128.rsp", "ECBGFSbox128.rsp");

        public static TheoryData<EncryptionTestVector> EncryptMultiTestVectors =
            RspReader.ReadEncryptionTestVectors(true, "ECBMMT128.rsp");

        public static TheoryData<EncryptionTestVector> DecryptMultiTestVectors =
            RspReader.ReadEncryptionTestVectors(false, "ECBMMT128.rsp");

        [Theory]
        [MemberData(nameof(EncryptTestVectors))]
        public static void Encrypt(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateEcbEncryptor(tv.Key, true));
        }

        [Theory]
        [MemberData(nameof(DecryptTestVectors))]
        public static void Decrypt(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateEcbDecryptor(tv.Key, true));
        }

        [Theory]
        [MemberData(nameof(EncryptMultiTestVectors))]
        public static void EncryptMulti(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateEcbEncryptor(tv.Key, true));
        }

        [Theory]
        [MemberData(nameof(DecryptMultiTestVectors))]
        public static void DecryptMulti(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateEcbDecryptor(tv.Key, true));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(EncryptTestVectors))]
        public static void EncryptIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateEcbEncryptor(tv.Key));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(DecryptTestVectors))]
        public static void DecryptIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateEcbDecryptor(tv.Key));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(EncryptMultiTestVectors))]
        public static void EncryptMultiIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateEcbEncryptor(tv.Key));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(DecryptMultiTestVectors))]
        public static void DecryptMultiIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateEcbDecryptor(tv.Key));
        }
    }
}
