#if HAS_INTRINSICS
using System;
using LibHac.Crypto2.Detail;

namespace LibHac.Crypto2
{
    public class AesEcbEncryptorNi : ICipher
    {
        private AesEcbModeNi _baseCipher;

        public AesEcbEncryptorNi(ReadOnlySpan<byte> key)
        {
            _baseCipher = new AesEcbModeNi();
            _baseCipher.Initialize(key, false);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Encrypt(input, output);
        }
    }

    public class AesEcbDecryptorNi : ICipher
    {
        private AesEcbModeNi _baseCipher;

        public AesEcbDecryptorNi(ReadOnlySpan<byte> key)
        {
            _baseCipher = new AesEcbModeNi();
            _baseCipher.Initialize(key, true);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Decrypt(input, output);
        }
    }
}
#endif
