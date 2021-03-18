using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Common;
using LibHac.Util;

namespace LibHac.Crypto.Impl
{
    public struct AesCtrMode
    {
        private AesCore _aesCore;
        public Buffer16 Iv;

        public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            Debug.Assert(iv.Length == Aes.BlockSize);

            _aesCore = new AesCore();
            _aesCore.Initialize(key, ReadOnlySpan<byte>.Empty, CipherMode.ECB, false);

            Iv = Unsafe.ReadUnaligned<Buffer16>(ref MemoryMarshal.GetReference(iv));
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = BitUtil.DivideUp(input.Length, Aes.BlockSize);
            int length = blockCount * Aes.BlockSize;

            using var counterBuffer = new RentedArray<byte>(length);
            FillDecryptedCounter(Iv, counterBuffer.Span);

            _aesCore.Encrypt(counterBuffer.Array, counterBuffer.Array, length);
            Utilities.XorArrays(output, input, counterBuffer.Span);
        }

        private static void FillDecryptedCounter(Span<byte> counter, Span<byte> buffer)
        {
            Span<ulong> bufL = MemoryMarshal.Cast<byte, ulong>(buffer);
            Span<ulong> counterL = MemoryMarshal.Cast<byte, ulong>(counter);

            ulong hi = counterL[0];
            ulong lo = BinaryPrimitives.ReverseEndianness(counterL[1]);

            for (int i = 0; i < bufL.Length; i += 2)
            {
                bufL[i] = hi;
                bufL[i + 1] = BinaryPrimitives.ReverseEndianness(lo);
                lo++;
            }

            counterL[1] = BinaryPrimitives.ReverseEndianness(lo);
        }
    }
}
