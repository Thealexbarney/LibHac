using LibHac.Crypto;
using Xunit;

namespace LibHac.Tests.CryptoTests
{
    internal static class Common
    {
        internal static void CipherTestCore(byte[] inputData, byte[] expected, ICipher cipher)
        {
            var outputData = new byte[expected.Length];

            cipher.Transform(inputData, outputData);

            Assert.Equal(expected, outputData);
        }
    }
}
