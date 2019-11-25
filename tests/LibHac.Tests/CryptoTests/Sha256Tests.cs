using System;
using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    public class Sha256Tests
    {
        public static TheoryData<HashTestVector> TestVectors =
            RspReader.ReadHashTestVectors("SHA256ShortMsg.rsp", "SHA256LongMsg.rsp");

        [Theory]
        [MemberData(nameof(TestVectors))]
        public static void Encrypt(HashTestVector tv)
        {
            Common.HashTestCore(tv.Message.AsSpan(0, tv.LengthBytes), tv.Digest, Sha256.CreateSha256Generator());
        }
    }
}
