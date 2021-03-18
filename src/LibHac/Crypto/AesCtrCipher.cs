using System;
using LibHac.Common;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto
{
    public class AesCtrCipher : ICipherWithIv
    {
        private AesCtrMode _baseCipher;

        public ref Buffer16 Iv => ref _baseCipher.Iv;

        public AesCtrCipher(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _baseCipher = new AesCtrMode();
            _baseCipher.Initialize(key, iv);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _baseCipher.Transform(input, output);
        }
    }
}
