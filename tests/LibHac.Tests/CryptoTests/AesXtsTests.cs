using System;
using System.Collections.Generic;
using System.Linq;
using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests;

public class AesXtsTests
{
    public static EncryptionTestVector[] EncryptTestVectors =
        RemovePartialByteTests(RspReader.ReadEncryptionTestVectorsArray(true, "XTSGenAES128.rsp"));

    public static EncryptionTestVector[] DecryptTestVectors =
        RemovePartialByteTests(RspReader.ReadEncryptionTestVectorsArray(false, "XTSGenAES128.rsp"));

    // The XTS implementation only supports multiples of whole bytes
    private static EncryptionTestVector[] RemovePartialByteTests(EncryptionTestVector[] input)
    {
        IEnumerable<EncryptionTestVector> filteredTestVectors = input
            .Where(x => x.DataUnitLength % 8 == 0);

        var output = new List<EncryptionTestVector>();

        foreach (EncryptionTestVector item in filteredTestVectors)
        {
            output.Add(item);
        }

        return output.ToArray();
    }

    [Fact]
    public static void Encrypt()
    {
        Common.EncryptCipherTest(EncryptTestVectors,
            (key, iv) => Aes.CreateXtsEncryptor(key.AsSpan(0, 0x10), key.AsSpan(0x10, 0x10), iv, true));
    }

    [Fact]
    public static void Decrypt()
    {
        Common.DecryptCipherTest(DecryptTestVectors,
            (key, iv) => Aes.CreateXtsDecryptor(key.AsSpan(0, 0x10), key.AsSpan(0x10, 0x10), iv, true));
    }

    [AesIntrinsicsRequiredFact]
    public static void EncryptIntrinsics()
    {
        Common.EncryptCipherTest(EncryptTestVectors,
            (key, iv) => Aes.CreateXtsEncryptor(key.AsSpan(0, 0x10), key.AsSpan(0x10, 0x10), iv));
    }

    [AesIntrinsicsRequiredFact]
    public static void DecryptIntrinsics()
    {
        Common.DecryptCipherTest(DecryptTestVectors,
            (key, iv) => Aes.CreateXtsDecryptor(key.AsSpan(0, 0x10), key.AsSpan(0x10, 0x10), iv));
    }


    // The above tests run all the test vectors in a single test to avoid having thousands of tests.
    // Use the below tests if running each test vector as an individual test is needed.

    // ReSharper disable InconsistentNaming

#pragma warning disable xUnit1013 // Public method should be marked as test

    public static TheoryData<EncryptionTestVector> EncryptTestVectors_Individual =
        RemovePartialByteTests(RspReader.ReadEncryptionTestVectors(true, "XTSGenAES128.rsp"));

    public static TheoryData<EncryptionTestVector> DecryptTestVectors_Individual =
        RemovePartialByteTests(RspReader.ReadEncryptionTestVectors(false, "XTSGenAES128.rsp"));

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

    //[Theory, MemberData(nameof(EncryptTestVectors_Individual))]
    public static void Encrypt_Individual(EncryptionTestVector tv)
    {
        Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
        Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

        Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateXtsEncryptor(key1, key2, tv.Iv, true));
    }

    //[Theory, MemberData(nameof(DecryptTestVectors_Individual))]
    public static void Decrypt_Individual(EncryptionTestVector tv)
    {
        Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
        Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

        Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateXtsDecryptor(key1, key2, tv.Iv, true));
    }

    //[AesIntrinsicsRequiredTheory, MemberData(nameof(EncryptTestVectors_Individual))]
    public static void EncryptIntrinsics_Individual(EncryptionTestVector tv)
    {
        Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
        Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

        Common.CipherTestCore(tv.PlainText, tv.CipherText, Aes.CreateXtsEncryptor(key1, key2, tv.Iv));
    }

    //[AesIntrinsicsRequiredTheory, MemberData(nameof(DecryptTestVectors_Individual))]
    public static void DecryptIntrinsics_Individual(EncryptionTestVector tv)
    {
        Span<byte> key1 = tv.Key.AsSpan(0, 0x10);
        Span<byte> key2 = tv.Key.AsSpan(0x10, 0x10);

        Common.CipherTestCore(tv.CipherText, tv.PlainText, Aes.CreateXtsDecryptor(key1, key2, tv.Iv));
    }
}