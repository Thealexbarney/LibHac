using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using LibHac.Common;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto
{
    public class AesCtrCipherNi : ICipherWithIv
    {
        private AesCtrModeNi _baseCipher;

        public ref Buffer16 Iv => ref Unsafe.As<Vector128<byte>, Buffer16>(ref _baseCipher.Iv);

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
