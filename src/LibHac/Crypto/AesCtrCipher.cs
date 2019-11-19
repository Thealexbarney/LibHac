using System;
using LibHac.Crypto.Detail;

namespace LibHac.Crypto
{
    public class AesCtrCipher : ICipher
    {
        private AesCtrMode _baseCipher;

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
