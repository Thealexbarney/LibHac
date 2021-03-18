using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using LibHac.Common;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto
{
    public class AesXtsEncryptorNi : ICipherWithIv
    {
        private AesXtsModeNi _baseCipher;

        public ref Buffer16 Iv => ref Unsafe.As<Vector128<byte>, Buffer16>(ref _baseCipher.Iv);

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

    public class AesXtsDecryptorNi : ICipherWithIv
    {
        private AesXtsModeNi _baseCipher;

        public ref Buffer16 Iv => ref Unsafe.As<Vector128<byte>, Buffer16>(ref _baseCipher.Iv);

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
