using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class AesCbcTests
    {
        public static EncryptionTestVector[] EncryptTestVectors =
            RspReader.ReadEncryptionTestVectorsArray(true, "CBCVarKey128.rsp", "CBCVarTxt128.rsp", "CBCKeySbox128.rsp", "CBCGFSbox128.rsp");

        public static EncryptionTestVector[] DecryptTestVectors =
            RspReader.ReadEncryptionTestVectorsArray(false, "CBCVarKey128.rsp", "CBCVarTxt128.rsp", "CBCKeySbox128.rsp", "CBCGFSbox128.rsp");

        public static EncryptionTestVector[] EncryptMultiTestVectors =
            RspReader.ReadEncryptionTestVectorsArray(true, "CBCMMT128.rsp");

        public static EncryptionTestVector[] DecryptMultiTestVectors =
            RspReader.ReadEncryptionTestVectorsArray(false, "CBCMMT128.rsp");

        [Fact]
        public static void Encrypt()
        {
            Common.EncryptCipherTest(EncryptTestVectors, (key, iv) => Aes.CreateCbcEncryptor(key, iv, true));
        }

        [Fact]
        public static void Decrypt()
        {
            Common.DecryptCipherTest(DecryptTestVectors, (key, iv) => Aes.CreateCbcDecryptor(key, iv, true));
        }

        [Fact]
        public static void EncryptMulti()
        {
            Common.EncryptCipherTest(EncryptMultiTestVectors, (key, iv) => Aes.CreateCbcEncryptor(key, iv, true));
        }

        [Fact]
        public static void DecryptMulti()
        {
            Common.DecryptCipherTest(DecryptMultiTestVectors, (key, iv) => Aes.CreateCbcDecryptor(key, iv, true));
        }

        [AesIntrinsicsRequiredFact]
        public static void EncryptIntrinsics()
        {
            Common.EncryptCipherTest(EncryptTestVectors, (key, iv) => Aes.CreateCbcEncryptor(key, iv));
        }

        [AesIntrinsicsRequiredFact]
        public static void DecryptIntrinsics()
        {
            Common.DecryptCipherTest(DecryptTestVectors, (key, iv) => Aes.CreateCbcDecryptor(key, iv));
        }

        [AesIntrinsicsRequiredFact]
        public static void EncryptMultiIntrinsics()
        {
            Common.EncryptCipherTest(EncryptMultiTestVectors, (key, iv) => Aes.CreateCbcEncryptor(key, iv));
        }

        [AesIntrinsicsRequiredFact]
        public static void DecryptMultiIntrinsics()
        {
            Common.DecryptCipherTest(DecryptMultiTestVectors, (key, iv) => Aes.CreateCbcDecryptor(key, iv));
        }


        // The above tests run all the test vectors in a single test to avoid having thousands of tests.
        // Use the below tests if running each test vector as an individual test is needed.

        // ReSharper disable InconsistentNaming

#pragma warning disable xUnit1013 // Public method should be marked as test

        public static TheoryData<EncryptionTestVector> EncryptTestVectors_Individual =
            RspReader.ReadEncryptionTestVectors(true, "CBCVarKey128.rsp", "CBCVarTxt128.rsp", "CBCKeySbox128.rsp", "CBCGFSbox128.rsp");

        public static TheoryData<EncryptionTestVector> DecryptTestVectors_Individual =
            RspReader.ReadEncryptionTestVectors(false, "CBCVarKey128.rsp", "CBCVarTxt128.rsp", "CBCKeySbox128.rsp", "CBCGFSbox128.rsp");

        public static TheoryData<EncryptionTestVector> EncryptMultiTestVectors_Individual =
            RspReader.ReadEncryptionTestVectors(true, "CBCMMT128.rsp");

        public static TheoryData<EncryptionTestVector> DecryptMultiTestVectors_Individual =
            RspReader.ReadEncryptionTestVectors(false, "CBCMMT128.rsp");

        //[Theory, MemberData(nameof(EncryptTestVectors_Individual))]
        public static void Encrypt_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv, true));
        }

        //[Theory, MemberData(nameof(DecryptTestVectors_Individual))]
        public static void Decrypt_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv, true));
        }

        //[Theory, MemberData(nameof(EncryptMultiTestVectors_Individual))]
        public static void EncryptMulti_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv, true));
        }

        //[Theory, MemberData(nameof(DecryptMultiTestVectors_Individual))]
        public static void DecryptMulti_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv, true));
        }

        //[AesIntrinsicsRequiredTheory, MemberData(nameof(EncryptTestVectors_Individual))]
        public static void EncryptIntrinsics_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv));
        }

        //[AesIntrinsicsRequiredTheory, MemberData(nameof(DecryptTestVectors_Individual))]
        public static void DecryptIntrinsics_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv));
        }

        //[AesIntrinsicsRequiredTheory, MemberData(nameof(EncryptMultiTestVectors_Individual))]
        public static void EncryptMultiIntrinsics_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateCbcEncryptor(tv.Key, tv.Iv));
        }

        //[AesIntrinsicsRequiredTheory, MemberData(nameof(DecryptMultiTestVectors_Individual))]
        public static void DecryptMultiIntrinsics_Individual(EncryptionTestVector tv)
        {
            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateCbcDecryptor(tv.Key, tv.Iv));
        }
    }
}
