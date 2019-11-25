using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class AesCbcTests
    {
        public static TheoryData<EncryptionTestVector> EncryptTestVectors =
            RspReader.ReadEncryptionTestVectors(true, "CBCVarKey128.rsp", "CBCVarTxt128.rsp", "CBCKeySbox128.rsp", "CBCGFSbox128.rsp");

        public static TheoryData<EncryptionTestVector> DecryptTestVectors =
            RspReader.ReadEncryptionTestVectors(false, "CBCVarKey128.rsp", "CBCVarTxt128.rsp", "CBCKeySbox128.rsp", "CBCGFSbox128.rsp");

        public static TheoryData<EncryptionTestVector> EncryptMultiTestVectors =
            RspReader.ReadEncryptionTestVectors(true, "CBCMMT128.rsp");

        public static TheoryData<EncryptionTestVector> DecryptMultiTestVectors =
            RspReader.ReadEncryptionTestVectors(false, "CBCMMT128.rsp");

        [Theory]
        [MemberData(nameof(EncryptTestVectors))]
        public static void Encrypt(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv, true));
        }

        [Theory]
        [MemberData(nameof(DecryptTestVectors))]
        public static void Decrypt(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv, true));
        }

        [Theory]
        [MemberData(nameof(EncryptMultiTestVectors))]
        public static void EncryptMulti(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv, true));
        }

        [Theory]
        [MemberData(nameof(DecryptMultiTestVectors))]
        public static void DecryptMulti(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv, true));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(EncryptTestVectors))]
        public static void EncryptIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(DecryptTestVectors))]
        public static void DecryptIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(EncryptMultiTestVectors))]
        public static void EncryptMultiIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(DecryptMultiTestVectors))]
        public static void DecryptMultiIntrinsics(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv));
        }
    }
}
