using System;

namespace LibHac.Crypto.Impl
{
    public struct AesEcbModeNi
    {
        private AesCoreNi _aesCore;

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
