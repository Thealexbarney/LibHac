using System;
using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    internal static class Common
    {
        internal static void CipherTestCore(byte[] inputData, byte[] expected, ICipher cipher)
        {
            byte[] transformBuffer = new byte[inputData.Length];
            Buffer.BlockCopy(inputData, 0, transformBuffer, 0, inputData.Length);

            cipher.Transform(transformBuffer, transformBuffer);

            Assert.Equal(expected, transformBuffer);
        }

        internal static void HashTestCore(ReadOnlySpan<byte> message, byte[] expectedDigest, IHash hash)
        {

            byte[] digestBuffer = new byte[Sha256.DigestSize];

            hash.Initialize();
            hash.Update(message);
            hash.GetHash(digestBuffer);

            Assert.Equal(expectedDigest, digestBuffer);
        }
    }
}
