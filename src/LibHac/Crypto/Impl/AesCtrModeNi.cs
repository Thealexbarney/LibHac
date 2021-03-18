using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Crypto.Impl
{
    public struct AesCtrModeNi
    {
        private AesCoreNi _aesCore;

        public Vector128<byte> Iv;

        public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            Debug.Assert(iv.Length == Aes.BlockSize);

            _aesCore.Initialize(key, false);

            Iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int remaining = Math.Min(input.Length, output.Length);
            int blockCount = remaining >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> byteSwapMask = Vector128.Create((ulong)0x706050403020100, 0x8090A0B0C0D0E0F).AsByte();
            var inc = Vector128.Create((ulong)0, 1);

            Vector128<byte> iv = Iv;
            Vector128<ulong> bSwappedIv = Ssse3.Shuffle(iv, byteSwapMask).AsUInt64();

            while (remaining >= 8 * Aes.BlockSize)
            {
                Vector128<byte> b0 = iv;

                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                Vector128<byte> b1 = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                Vector128<byte> b2 = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                Vector128<byte> b3 = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                Vector128<byte> b4 = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                Vector128<byte> b5 = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                Vector128<byte> b6 = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                Vector128<byte> b7 = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                _aesCore.EncryptBlocks8(b0, b1, b2, b3, b4, b5, b6, b7,
                    out b0, out b1, out b2, out b3, out b4, out b5, out b6, out b7);

                Unsafe.Add(ref outBlock, 0) = Sse2.Xor(Unsafe.Add(ref inBlock, 0), b0);
                Unsafe.Add(ref outBlock, 1) = Sse2.Xor(Unsafe.Add(ref inBlock, 1), b1);
                Unsafe.Add(ref outBlock, 2) = Sse2.Xor(Unsafe.Add(ref inBlock, 2), b2);
                Unsafe.Add(ref outBlock, 3) = Sse2.Xor(Unsafe.Add(ref inBlock, 3), b3);
                Unsafe.Add(ref outBlock, 4) = Sse2.Xor(Unsafe.Add(ref inBlock, 4), b4);
                Unsafe.Add(ref outBlock, 5) = Sse2.Xor(Unsafe.Add(ref inBlock, 5), b5);
                Unsafe.Add(ref outBlock, 6) = Sse2.Xor(Unsafe.Add(ref inBlock, 6), b6);
                Unsafe.Add(ref outBlock, 7) = Sse2.Xor(Unsafe.Add(ref inBlock, 7), b7);

                // Increase the counter
                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                iv = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                inBlock = ref Unsafe.Add(ref inBlock, 8);
                outBlock = ref Unsafe.Add(ref outBlock, 8);
                remaining -= 8 * Aes.BlockSize;
            }

            while (remaining >= Aes.BlockSize)
            {
                Vector128<byte> encIv = _aesCore.EncryptBlock(iv);
                outBlock = Sse2.Xor(inBlock, encIv);

                // Increase the counter
                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                iv = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
                remaining -= Aes.BlockSize;
            }

            Iv = iv;

            if (remaining != 0)
            {
                EncryptCtrPartialBlock(input.Slice(blockCount * 0x10), output.Slice(blockCount * 0x10));
            }
        }

        private void EncryptCtrPartialBlock(ReadOnlySpan<byte> input, Span<byte> output)
        {
            Span<byte> counter = stackalloc byte[0x10];
            Unsafe.WriteUnaligned(ref counter[0], Iv);

            _aesCore.Encrypt(counter, counter);

            input.CopyTo(output);
            Utilities.XorArrays(output, counter);

            for (int i = 0; i < counter.Length; i++)
            {
                if (++counter[i] != 0) break;
            }

            Unsafe.ReadUnaligned<Vector128<byte>>(ref counter[0]);
        }
    }
}
