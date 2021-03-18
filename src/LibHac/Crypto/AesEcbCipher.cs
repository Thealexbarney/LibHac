using System;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto
{
    public class AesEcbEncryptor : ICipher
    {
        private AesEcbMode _baseCipher;

        public AesEcbEncryptor(ReadOnlySpan<byte> key)
        {
            _baseCipher = new AesEcbMode();
            _baseCipher.Initialize(key, false);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Encrypt(input, output);
        }
    }

    public class AesEcbDecryptor : ICipher
    {
        private AesEcbMode _baseCipher;

        public AesEcbDecryptor(ReadOnlySpan<byte> key)
        {
            _baseCipher = new AesEcbMode();
            _baseCipher.Initialize(key, true);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Decrypt(input, output);
        }
    }
}
