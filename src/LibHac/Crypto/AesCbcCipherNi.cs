#if HAS_INTRINSICS
using System;
using LibHac.Crypto.Detail;

namespace LibHac.Crypto
{
    public struct AesCbcEncryptorNi : ICipher
    {
        private AesCbcModeNi _baseCipher;

        public AesCbcEncryptorNi(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesCbcModeNi();
            _baseCipher.Initialize(key, iv, false);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Encrypt(input, output);
        }
    }

    public struct AesCbcDecryptorNi : ICipher
    {
        private AesCbcModeNi _baseCipher;

        public AesCbcDecryptorNi(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesCbcModeNi();
            _baseCipher.Initialize(key, iv, true);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Decrypt(input, output);
        }
    }
}
#endif