using System;
using System.Collections.Generic;
using System.Linq;
using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class AesXtsTests
    {
        public static TheoryData<EncryptionTestVector> EncryptTestVectors =
            RemovePartialByteTests(RspReader.ReadEncryptionTestVectors(true, "XTSGenAES128.rsp"));

        public static TheoryData<EncryptionTestVector> DecryptTestVectors =
            RemovePartialByteTests(RspReader.ReadEncryptionTestVectors(false, "XTSGenAES128.rsp"));

        // The XTS implementation only supports multiples of whole bytes
        private static TheoryData<EncryptionTestVector> RemovePartialByteTests(TheoryData<EncryptionTestVector> input)
        {
            IEnumerable<EncryptionTestVector> filteredTestVectors = input
                .Select(x => x[0])
                .Cast<EncryptionTestVector>()
                .Where(x => x.DataUnitLength % 8 == 0);

            var output = new TheoryData<EncryptionTestVector>();

            foreach (EncryptionTestVector item in filteredTestVectors)
            {
                output.Add(item);
            }

            return output;
        }

        [Theory]
        [MemberData(nameof(EncryptTestVectors))]
        public static void Encrypt(EncryptionTestVector tv)
        {
            Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
            Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateXtsEncryptor(key1, key2, tv.Iv, true));
        }

        [Theory]
        [MemberData(nameof(DecryptTestVectors))]
        public static void Decrypt(EncryptionTestVector tv)
        {
            Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
            Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateXtsDecryptor(key1, key2, tv.Iv, true));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(EncryptTestVectors))]
        public static void EncryptIntrinsics(EncryptionTestVector tv)
        {
            Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
            Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

            Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateXtsEncryptor(key1, key2, tv.Iv));
        }

        [AesIntrinsicsRequiredTheory]
        [MemberData(nameof(DecryptTestVectors))]
        public static void DecryptIntrinsics(EncryptionTestVector tv)
        {
            Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
            Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

            Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateXtsDecryptor(key1, key2, tv.Iv));
        }
    }
}
