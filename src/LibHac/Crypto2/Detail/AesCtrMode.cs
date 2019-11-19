using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Common;

namespace LibHac.Crypto2.Detail
{
    public struct AesCtrMode
    {
        private AesCore _aesCore;
        private byte[] _counter;

        public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            Debug.Assert(iv.Length == Aes.BlockSize);

            _aesCore = new AesCore();
            _aesCore.Initialize(key, ReadOnlySpan<byte>.Empty, CipherMode.ECB, false);

            _counter = iv.ToArray();
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Util.DivideByRoundUp(input.Length, Aes.BlockSize);
            int length = blockCount * Aes.BlockSize;

            using var counterBuffer = new RentedArray<byte>(length);
            FillDecryptedCounter(_counter, counterBuffer.Span);

            _aesCore.Encrypt(counterBuffer.Array, counterBuffer.Array, length);
            Util.XorArrays(output, input, counterBuffer.Span);
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
