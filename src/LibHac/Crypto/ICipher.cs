using System;

namespace LibHac.Crypto
{
    public interface ICipher
    {
        void Transform(ReadOnlySpan<byte> input, Span<byte> output);
    }
}