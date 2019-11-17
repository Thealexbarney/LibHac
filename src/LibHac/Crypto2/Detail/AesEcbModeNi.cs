#if HAS_INTRINSICS
using System;

namespace LibHac.Crypto2.Detail
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
            _aesCore.Encrypt(input, output);
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _aesCore.Decrypt(input, output);
        }
    }
}
#endif
