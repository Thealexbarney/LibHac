using System;
using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;

namespace LibHac.Crypto2
{
    public class AesXtsCipher : ICipher
    {
        private ICipher _dataCipher;
        private ICipher _tweakCipher;
        private Buffer16 _iv;
        private bool _decrypting;

        public AesXtsCipher(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool decrypting)
        {
            Debug.Assert(key1.Length == AesCrypto.KeySize128);
            Debug.Assert(key2.Length == AesCrypto.KeySize128);
            Debug.Assert(iv.Length == AesCrypto.KeySize128);

            if (decrypting)
            {
                _dataCipher = new AesEcbDecryptor(key1);
            }
            else
            {
                _dataCipher = new AesEcbEncryptor(key1);
            }

            _tweakCipher = new AesEcbEncryptor(key2);

            _iv = new Buffer16();
            iv.CopyTo(_iv);

            _decrypting = decrypting;
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int blockCount = length >> 4;
            int leftover = length & 0xF;

            // Data units must be at least 1 block long.
            if (length < AesCrypto.BlockSize)
                throw new ArgumentException();

            var tweak = new Buffer16();

            _tweakCipher.Transform(_iv, tweak);

            byte[] tweakBufferRented = ArrayPool<byte>.Shared.Rent(blockCount * AesCrypto.BlockSize);
            try
            {
                Span<byte> tweakBuffer = tweakBufferRented.AsSpan(0, blockCount * AesCrypto.BlockSize);
                tweak = FillTweakBuffer(tweak, MemoryMarshal.Cast<byte, Buffer16>(tweakBuffer));

                Util.XorArrays(output, input, tweakBuffer);
                _dataCipher.Transform(output.Slice(0, blockCount * AesCrypto.BlockSize), output);
                Util.XorArrays(output, output, tweakBuffer);
            }
            finally { ArrayPool<byte>.Shared.Return(tweakBufferRented); }

            if (leftover != 0)
            {
                ref Buffer16 inBlock =
                    ref Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(input)), blockCount);

                ref Buffer16 outBlock =
                    ref Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(output)), blockCount);

                ref Buffer16 prevOutBlock = ref Unsafe.Subtract(ref outBlock, 1);

                var tmp = new Buffer16();

                for (int i = 0; i < leftover; i++)
                {
                    outBlock[i] = prevOutBlock[i];
                    tmp[i] = inBlock[i];
                }

                for (int i = leftover; i < AesCrypto.BlockSize; i++)
                {
                    tmp[i] = prevOutBlock[i];
                }

                XorBuffer(ref tmp, ref tmp, ref tweak);
                _dataCipher.Transform(tmp, tmp);
                XorBuffer(ref prevOutBlock, ref tmp, ref tweak);
            }
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int blockCount = length >> 4;
            int leftover = length & 0xF;

            // Data units must be at least 1 block long.
            if (length < AesCrypto.BlockSize)
                throw new ArgumentException();

            if (leftover != 0) blockCount--;

            var tweak = new Buffer16();

            _tweakCipher.Transform(_iv, tweak);

            if (blockCount > 0)
            {
                byte[] tweakBufferRented = ArrayPool<byte>.Shared.Rent(blockCount * AesCrypto.BlockSize);
                try
                {
                    Span<byte> tweakBuffer = tweakBufferRented.AsSpan(0, blockCount * AesCrypto.BlockSize);
                    tweak = FillTweakBuffer(tweak, MemoryMarshal.Cast<byte, Buffer16>(tweakBuffer));

                    Util.XorArrays(output, input, tweakBuffer);
                    _dataCipher.Transform(output.Slice(0, blockCount * AesCrypto.BlockSize), output);
                    Util.XorArrays(output, output, tweakBuffer);
                }
                finally { ArrayPool<byte>.Shared.Return(tweakBufferRented); }
            }

            if (leftover != 0)
            {
                Buffer16 finalTweak = tweak;
                Gf128Mul(ref finalTweak);

                ref Buffer16 inBlock =
                    ref Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(input)), blockCount);

                ref Buffer16 outBlock =
                    ref Unsafe.Add(ref Unsafe.As<byte, Buffer16>(ref MemoryMarshal.GetReference(output)), blockCount);

                var tmp = new Buffer16();

                XorBuffer(ref tmp, ref inBlock, ref finalTweak);
                _dataCipher.Transform(tmp, tmp);
                XorBuffer(ref outBlock, ref tmp, ref finalTweak);

                ref Buffer16 finalOutBlock = ref Unsafe.Add(ref outBlock, 1);
                ref Buffer16 finalInBlock = ref Unsafe.Add(ref inBlock, 1);

                for (int i = 0; i < leftover; i++)
                {
                    finalOutBlock[i] = outBlock[i];
                    tmp[i] = finalInBlock[i];
                }

                for (int i = leftover; i < AesCrypto.BlockSize; i++)
                {
                    tmp[i] = outBlock[i];
                }

                XorBuffer(ref tmp, ref tmp, ref tweak);
                _dataCipher.Transform(tmp, tmp);
                XorBuffer(ref outBlock, ref tmp, ref tweak);
            }
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            if (_decrypting)
            {
                Decrypt(input, output);
            }
            else
            {
                Encrypt(input, output);
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
