#if HAS_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Crypto2
{
    public class AesCtrEncryptorHw : ICipher
    {
        private AesCoreNi _aesCore;
        private Vector128<byte> _iv;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public AesCtrEncryptorHw(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _aesCore = new AesCoreNi();
            _aesCore.Initialize(key, false);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            ReadOnlySpan<Vector128<byte>> keys = _aesCore.RoundKeys;
            ReadOnlySpan<Vector128<byte>> inBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(input);
            Span<Vector128<byte>> outBlocks = MemoryMarshal.Cast<byte, Vector128<byte>>(output);

            Vector128<byte> byteSwapMask = Vector128.Create((ulong)0x706050403020100, 0x8090A0B0C0D0E0F).AsByte();
            Vector128<ulong> inc = Vector128.Create((ulong)0, 1);

            var iv = _iv;
            Vector128<ulong> bSwappedIv = Ssse3.Shuffle(iv, byteSwapMask).AsUInt64();

            for (int i = 0; i < inBlocks.Length; i++)
            {
                Vector128<byte> b = Sse2.Xor(iv, keys[0]);
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

                outBlocks[i] = Sse2.Xor(inBlocks[i], b);

                // Increase the counter
                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                iv = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);
            }

            _iv = iv;

            if ((input.Length & 0xF) != 0)
            {
                EncryptCtrPartialBlock(input.Slice(inBlocks.Length * 0x10), output.Slice(outBlocks.Length * 0x10));
            }
        }

        private void EncryptCtrPartialBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            Span<byte> counter = stackalloc byte[0x10];
            Unsafe.WriteUnaligned(ref counter[0], _iv);

            _aesCore.Encrypt(counter, counter);

            input.CopyTo(output);
            Util.XorArrays(output, counter);

            for (int i = 0; i < counter.Length; i++)
            {
                if (++counter[i] != 0) break;
            }

            Unsafe.ReadUnaligned<Vector128<byte>>(ref counter[0]);
        }
    }
}
#endif
