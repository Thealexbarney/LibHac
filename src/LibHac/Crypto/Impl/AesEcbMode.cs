using System;
using System.Security.Cryptography;

namespace LibHac.Crypto.Impl
{
    public struct AesEcbMode
    {
        private AesCore _aesCore;

        public void Initialize(ReadOnlySpan<byte> key, bool isDecrypting)
        {
            _aesCore = new AesCore();
            _aesCore.Initialize(key, ReadOnlySpan<byte>.Empty, CipherMode.ECB, isDecrypting);
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _aesCore.Encrypt(input, output);
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _aesCore.Decrypt(input, output);
        }
    }
}
