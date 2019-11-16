#if HAS_INTRINSICS
using System;

namespace LibHac.Crypto2
{
    public class AesEcbEncryptorHw : ICipher
    {
        private AesCoreNi _aesCore;

        public AesEcbEncryptorHw(ReadOnlySpan<byte> key)
        {
            _aesCore = new AesCoreNi();
            _aesCore.Initialize(key, false);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _aesCore.Encrypt(input, output);
        }
    }

    public class AesEcbDecryptorHw : ICipher
    {
        private AesCoreNi _aesCore;

        public AesEcbDecryptorHw(ReadOnlySpan<byte> key)
        {
            _aesCore = new AesCoreNi();
            _aesCore.Initialize(key, true);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            _aesCore.Decrypt(input, output);
        }
    }
}
#endif
