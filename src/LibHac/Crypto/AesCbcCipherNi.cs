using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using LibHac.Common;
using LibHac.Crypto.Impl;

namespace LibHac.Crypto
{
    public class AesCbcEncryptorNi : ICipherWithIv
    {
        private AesCbcModeNi _baseCipher;

        public ref Buffer16 Iv => ref Unsafe.As<Vector128<byte>, Buffer16>(ref _baseCipher.Iv);

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

    public class AesCbcDecryptorNi : ICipherWithIv
    {
        private AesCbcModeNi _baseCipher;

        public ref Buffer16 Iv => ref Unsafe.As<Vector128<byte>, Buffer16>(ref _baseCipher.Iv);

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
