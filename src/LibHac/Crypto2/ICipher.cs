using System;

namespace LibHac.Crypto2
{
    public interface ICipher
    {
        void Transform(ReadOnlySpan<byte> input, Span<byte> output);
    }
}