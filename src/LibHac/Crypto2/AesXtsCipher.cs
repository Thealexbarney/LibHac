using System;
using LibHac.Crypto2.Detail;

namespace LibHac.Crypto2
{
    public class AesXtsEncryptor : ICipher
    {
        private AesXtsMode _baseCipher;

        public AesXtsEncryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesXtsMode();
            _baseCipher.Initialize(key1, key2, iv, false);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Encrypt(input, output);
        }
    }

    public class AesXtsDecryptor : ICipher
    {
        private AesXtsMode _baseCipher;

        public AesXtsDecryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesXtsMode();
            _baseCipher.Initialize(key1, key2, iv, true);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Decrypt(input, output);
        }
    }
}
