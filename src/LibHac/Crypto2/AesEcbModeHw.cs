

#if HAS_INTRINSICS
using System;
using System.Runtime.Intrinsics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;

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
            ReadOnlySpan<Vector128<byte>> keys = _aesCore.RoundKeys;
            ReadOnlySpan<Vector128<byte>> inBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(input);
            Span<Vector128<byte>> outBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(output);

            for (int i = 0; i < inBlocks.Length; i++)
            {
                Vector128<byte> b = inBlocks[i];

                b = Sse2.Xor(b, keys[0]);
                b = Aes.Encrypt(b, keys[1]);
                b = Aes.Encrypt(b, keys[2]);
                b = Aes.Encrypt(b, keys[3]);
                b = Aes.Encrypt(b, keys[4]);
                b = Aes.Encrypt(b, keys[5]);
                b = Aes.Encrypt(b, keys[6]);
                b = Aes.Encrypt(b, keys[7]);
                b = Aes.Encrypt(b, keys[8]);
                b = Aes.Encrypt(b, keys[9]);
                b = Aes.EncryptLast(b, keys[10]);

                outBlocks[i] = b;
            }
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
