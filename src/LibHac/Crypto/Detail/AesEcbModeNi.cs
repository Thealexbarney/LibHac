#if HAS_INTRINSICS
using System;

namespace LibHac.Crypto.Detail
{
    public struct AesEcbModeNi
    {
#pragma warning disable 649
        private AesCoreNi _aesCore;
#pragma warning restore 649

        public void Initialize(ReadOnlySpan<byte> key, bool isDecrypting)
        {
            _aesCore.Initialize(key, isDecrypting);
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _aesCore.EncryptInterleaved8(input, output);
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _aesCore.DecryptInterleaved8(input, output);
        }
    }
}
#endif
