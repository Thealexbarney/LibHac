using System;
using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests;

public class Sha256Tests
{
    public static HashTestVector[] TestVectors =
        RspReader.ReadHashTestVectorsArray("SHA256ShortMsg.rsp", "SHA256LongMsg.rsp");

    [Fact]
    public static void Encrypt()
    {
        foreach (HashTestVector tv in TestVectors)
        {
            Common.HashTestCore(tv.Message.AsSpan(0, tv.LengthBytes), tv.Digest, Sha256.CreateSha256Generator());
        }
    }


    // The above tests run all the test vectors in a single test to avoid having thousands of tests.
    // Use the below tests if running each test vector as an individual test is needed.

    // ReSharper disable InconsistentNaming

#pragma warning disable xUnit1013 // Public method should be marked as test

    public static TheoryData<HashTestVector> TestVectors_Individual =
        RspReader.ReadHashTestVectors("SHA256ShortMsg.rsp", "SHA256LongMsg.rsp");

    //[Theory, MemberData(nameof(TestVectors_Individual))]
    public static void Encrypt_Individual(HashTestVector tv)
    {
        Common.HashTestCore(tv.Message.AsSpan(0, tv.LengthBytes), tv.Digest, Sha256.CreateSha256Generator());
    }
}