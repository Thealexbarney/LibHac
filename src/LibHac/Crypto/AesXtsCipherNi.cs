#if HAS_INTRINSICS
using System;
using LibHac.Crypto.Detail;

namespace LibHac.Crypto
{
    public class AesXtsEncryptorNi : ICipher
    {
        private AesXtsModeNi _baseCipher;

        public AesXtsEncryptorNi(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesXtsModeNi();
            _baseCipher.Initialize(key1, key2, iv, false);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Encrypt(input, output);
        }
    }

    public class AesXtsDecryptorNi : ICipher
    {
        private AesXtsModeNi _baseCipher;

        public AesXtsDecryptorNi(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesXtsModeNi();
            _baseCipher.Initialize(key1, key2, iv, true);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Decrypt(input, output);
        }
    }
}
#endif
