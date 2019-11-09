#if HAS_INTRINSICS
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Crypto2
{
    public struct AesCbcEncryptorHw : ICipher
    {
        private AesCoreNi _aesCore;
        private Vector128<byte> _iv;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public AesCbcEncryptorHw(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _aesCore = new AesCoreNi();
            _aesCore.Initialize(key, false);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            ReadOnlySpan<Vector128<byte>> keys = _aesCore.RoundKeys;
            ReadOnlySpan<Vector128<byte>> inBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(input);
            Span<Vector128<byte>> outBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(output);

            Vector128<byte> b = _iv;
            
            for (int i = 0; i < inBlocks.Length; i++)
            {
                b = Sse2.Xor(b, inBlocks[i]);

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

            _iv = b;
        }
    }

    public struct AesCbcDecryptorHw : ICipher
    {
        private AesCoreNi _aesCore;
        private Vector128<byte> _iv;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public AesCbcDecryptorHw(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _aesCore = new AesCoreNi();
            _aesCore.Initialize(key, true);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            ReadOnlySpan<Vector128<byte>> keys = _aesCore.RoundKeys;
            ReadOnlySpan<Vector128<byte>> inBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(input);
            Span<Vector128<byte>> outBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(output);

            Vector128<byte> iv = _iv;

            for (int i = 0; i < inBlocks.Length; i++)
            {
                Vector128<byte> b = inBlocks[i];
                Vector128<byte> nextIv = b;

                b = Sse2.Xor(b, keys[10]);
                b = Aes.Decrypt(b, keys[9]);
                b = Aes.Decrypt(b, keys[8]);
                b = Aes.Decrypt(b, keys[7]);
                b = Aes.Decrypt(b, keys[6]);
                b = Aes.Decrypt(b, keys[5]);
                b = Aes.Decrypt(b, keys[4]);
                b = Aes.Decrypt(b, keys[3]);
                b = Aes.Decrypt(b, keys[2]);
                b = Aes.Decrypt(b, keys[1]);
                b = Aes.DecryptLast(b, keys[0]);

                b = Sse2.Xor(b, iv);
                iv = nextIv;
                outBlocks[i] = b;
            }

            _iv = iv;
        }
    }
}
#endif