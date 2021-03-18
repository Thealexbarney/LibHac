using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Common;

namespace LibHac.Crypto.Impl
{
    public struct AesXtsMode
    {
        private AesCore _dataAesCore;
        private AesCore _tweakAesCore;
        public Buffer16 Iv;

        public void Initialize(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool isDecrypting)
        {
            Debug.Assert(iv.Length == Aes.BlockSize);

            _dataAesCore = new AesCore();
            _tweakAesCore = new AesCore();

            _dataAesCore.Initialize(key1, ReadOnlySpan<byte>.Empty, CipherMode.ECB, isDecrypting);
            _tweakAesCore.Initialize(key2, ReadOnlySpan<byte>.Empty, CipherMode.ECB, false);

            Iv = Unsafe.ReadUnaligned<Buffer16>(ref MemoryMarshal.GetReference(iv));
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int blockCount = length >> 4;
            int leftover = length & 0xF;

            // Data units must be at least 1 block long.
            if (length < Aes.BlockSize)
                throw new ArgumentException();

            var tweak = new Buffer16();

            _tweakAesCore.Encrypt(Iv, tweak);

            using var tweakBuffer = new RentedArray<byte>(blockCount * Aes.BlockSize);
            tweak = FillTweakBuffer(tweak, MemoryMarshal.Cast<byte, Buffer16>(tweakBuffer.Span));

            Utilities.XorArrays(output, input, tweakBuffer.Span);
            _dataAesCore.Encrypt(output.Slice(0, blockCount * Aes.BlockSize), output);
            Utilities.XorArrays(output, output, tweakBuffer.Array);

            if (leftover != 0)
            {
                Buffer16 inBlock =
                    Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(input)), blockCount);

                ref Buffer16 outBlock =
                    ref Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(output)), blockCount);

                ref Buffer16 prevOutBlock = ref Unsafe.Subtract(ref outBlock, 1);

                var tmp = new Buffer16();

                for (int i = 0; i < leftover; i++)
                {
                    outBlock[i] = prevOutBlock[i];
                    tmp[i] = inBlock[i];
                }

                for (int i = leftover; i < Aes.BlockSize; i++)
                {
                    tmp[i] = prevOutBlock[i];
                }

                XorBuffer(ref tmp, ref tmp, ref tweak);
                _dataAesCore.Encrypt(tmp, tmp);
                XorBuffer(ref prevOutBlock, ref tmp, ref tweak);
            }
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int blockCount = length >> 4;
            int leftover = length & 0xF;

            // Data units must be at least 1 block long.
            if (length < Aes.BlockSize)
                throw new ArgumentException();

            if (leftover != 0) blockCount--;

            var tweak = new Buffer16();

            _tweakAesCore.Encrypt(Iv, tweak);

            if (blockCount > 0)
            {
                using var tweakBuffer = new RentedArray<byte>(blockCount * Aes.BlockSize);
                tweak = FillTweakBuffer(tweak, MemoryMarshal.Cast<byte, Buffer16>(tweakBuffer.Span));

                Utilities.XorArrays(output, input, tweakBuffer.Span);
                _dataAesCore.Decrypt(output.Slice(0, blockCount * Aes.BlockSize), output);
                Utilities.XorArrays(output, output, tweakBuffer.Span);
            }

            if (leftover != 0)
            {
                Buffer16 finalTweak = tweak;
                Gf128Mul(ref finalTweak);

                Buffer16 inBlock =
                    Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(input)), blockCount);

                ref Buffer16 outBlock =
                    ref Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(output)), blockCount);

                var tmp = new Buffer16();

                XorBuffer(ref tmp, ref inBlock, ref finalTweak);
                _dataAesCore.Decrypt(tmp, tmp);
                XorBuffer(ref outBlock, ref tmp, ref finalTweak);

                ref Buffer16 finalOutBlock = ref Unsafe.Add(ref outBlock, 1);

                Buffer16 finalInBlock = Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(input)),
                    blockCount + 1);

                for (int i = 0; i < leftover; i++)
                {
                    finalOutBlock[i] = outBlock[i];
                    tmp[i] = finalInBlock[i];
                }

                for (int i = leftover; i < Aes.BlockSize; i++)
                {
                    tmp[i] = outBlock[i];
                }

                XorBuffer(ref tmp, ref tmp, ref tweak);
                _dataAesCore.Decrypt(tmp, tmp);
                XorBuffer(ref outBlock, ref tmp, ref tweak);
            }
        }

        private static Buffer16 FillTweakBuffer(Buffer16 initialTweak, Span<Buffer16> tweakBuffer)
        {
            for (int i = 0; i < tweakBuffer.Length; i++)
            {
                tweakBuffer[i] = initialTweak;
                Gf128Mul(ref initialTweak);
            }

            return initialTweak;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void Gf128Mul(ref Buffer16 buffer)
        {
            Span<ulong> b = buffer.AsSpan<ulong>();

            ulong tt = (ulong)((long)b[1] >> 63) & 0x87;

            b[1] = (b[1] << 1) | (b[0] >> 63);
            b[0] = (b[0] << 1) ^ tt;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void XorBuffer(ref Buffer16 output, ref Buffer16 input1, ref Buffer16 input2)
        {
            Span<ulong> outputS = output.AsSpan<ulong>();
            Span<ulong> input1S = input1.AsSpan<ulong>();
            Span<ulong> input2S = input2.AsSpan<ulong>();

            outputS[0] = input1S[0] ^ input2S[0];
            outputS[1] = input1S[1] ^ input2S[1];
        }
    }
}
