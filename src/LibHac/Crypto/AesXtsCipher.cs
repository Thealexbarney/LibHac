using System;
using LibHac.Common;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto
{
    public class AesXtsEncryptor : ICipherWithIv
    {
        private AesXtsMode _baseCipher;

        public ref Buffer16 Iv => ref  _baseCipher.Iv;

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

    public class AesXtsDecryptor : ICipherWithIv
    {
        private AesXtsMode _baseCipher;
        
        public ref Buffer16 Iv => ref _baseCipher.Iv;

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
