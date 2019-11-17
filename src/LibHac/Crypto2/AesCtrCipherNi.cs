#if HAS_INTRINSICS
using System;
using LibHac.Crypto2.Detail;

namespace LibHac.Crypto2
{
    public class AesCtrCipherNi : ICipher
    {
        private AesCtrModeNi _baseCipher;

        public AesCtrCipherNi(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesCtrModeNi();
            _baseCipher.Initialize(key, iv);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Transform(input, output);
        }
    }
}
#endif
