using System;
using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    internal static class Common
    {
        internal static void CipherTestCore(byte[] inputData, byte[] expected, ICipher cipher)
        {
            var transformBuffer = new byte[inputData.Length];
            Buffer.BlockCopy(inputData, 0, transformBuffer, 0, inputData.Length);

            cipher.Transform(transformBuffer, transformBuffer);

            Assert.Equal(expected, transformBuffer);
        }
    }
}
