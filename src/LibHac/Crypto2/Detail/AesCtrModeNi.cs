#if HAS_INTRINSICS
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Crypto2.Detail
{
    public struct AesCtrModeNi
    {
#pragma warning disable 649
        private AesCoreNi _aesCore;
#pragma warning restore 649

        private Vector128<byte> _iv;

        public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _aesCore.Initialize(key, false);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> byteSwapMask = Vector128.Create((ulong)0x706050403020100, 0x8090A0B0C0D0E0F).AsByte();
            Vector128<ulong> inc = Vector128.Create((ulong)0, 1);

            Vector128<byte> iv = _iv;
            Vector128<ulong> bSwappedIv = Ssse3.Shuffle(iv, byteSwapMask).AsUInt64();

            for (int i = 0; i < blockCount; i++)
            {
                Vector128<byte> encIv = _aesCore.EncryptBlock(iv);
                outBlock = Sse2.Xor(inBlock, encIv);

                // Increase the counter
                bSwappedIv = Sse2.Add(bSwappedIv, inc);
                iv = Ssse3.Shuffle(bSwappedIv.AsByte(), byteSwapMask);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }

            _iv = iv;

            if ((input.Length & 0xF) != 0)
            {
                EncryptCtrPartialBlock(input.Slice(blockCount * 0x10), output.Slice(blockCount * 0x10));
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
