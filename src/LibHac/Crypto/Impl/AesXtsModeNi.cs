using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using LibHac.Common;

namespace LibHac.Crypto.Impl
{
    public struct AesXtsModeNi
    {
        private AesCoreNi _dataAesCore;
        private AesCoreNi _tweakAesCore;

        public Vector128<byte> Iv;

        public void Initialize(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool decrypting)
        {
            Debug.Assert(iv.Length == Aes.KeySize128);

            _dataAesCore.Initialize(key1, decrypting);
            _tweakAesCore.Initialize(key2, false);

            Iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int remainingBlocks = length >> 4;
            int leftover = length & 0xF;

            Debug.Assert(remainingBlocks > 0);

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> mask = Vector128.Create(0x87, 1).AsByte();

            Vector128<byte> tweak = _tweakAesCore.EncryptBlock(Iv);

            while (remainingBlocks > 7)
            {
                Vector128<byte> b0 = Sse2.Xor(tweak, Unsafe.Add(ref inBlock, 0));

                Vector128<byte> tweak1 = Gf128Mul(tweak, mask);
                Vector128<byte> b1 = Sse2.Xor(tweak1, Unsafe.Add(ref inBlock, 1));

                Vector128<byte> tweak2 = Gf128Mul(tweak1, mask);
                Vector128<byte> b2 = Sse2.Xor(tweak2, Unsafe.Add(ref inBlock, 2));

                Vector128<byte> tweak3 = Gf128Mul(tweak2, mask);
                Vector128<byte> b3 = Sse2.Xor(tweak3, Unsafe.Add(ref inBlock, 3));

                Vector128<byte> tweak4 = Gf128Mul(tweak3, mask);
                Vector128<byte> b4 = Sse2.Xor(tweak4, Unsafe.Add(ref inBlock, 4));

                Vector128<byte> tweak5 = Gf128Mul(tweak4, mask);
                Vector128<byte> b5 = Sse2.Xor(tweak5, Unsafe.Add(ref inBlock, 5));

                Vector128<byte> tweak6 = Gf128Mul(tweak5, mask);
                Vector128<byte> b6 = Sse2.Xor(tweak6, Unsafe.Add(ref inBlock, 6));

                Vector128<byte> tweak7 = Gf128Mul(tweak6, mask);
                Vector128<byte> b7 = Sse2.Xor(tweak7, Unsafe.Add(ref inBlock, 7));

                _dataAesCore.EncryptBlocks8(b0, b1, b2, b3, b4, b5, b6, b7,
                    out b0, out b1, out b2, out b3, out b4, out b5, out b6, out b7);

                Unsafe.Add(ref outBlock, 0) = Sse2.Xor(tweak, b0);
                Unsafe.Add(ref outBlock, 1) = Sse2.Xor(tweak1, b1);
                Unsafe.Add(ref outBlock, 2) = Sse2.Xor(tweak2, b2);
                Unsafe.Add(ref outBlock, 3) = Sse2.Xor(tweak3, b3);
                Unsafe.Add(ref outBlock, 4) = Sse2.Xor(tweak4, b4);
                Unsafe.Add(ref outBlock, 5) = Sse2.Xor(tweak5, b5);
                Unsafe.Add(ref outBlock, 6) = Sse2.Xor(tweak6, b6);
                Unsafe.Add(ref outBlock, 7) = Sse2.Xor(tweak7, b7);

                tweak = Gf128Mul(tweak7, mask);

                inBlock = ref Unsafe.Add(ref inBlock, 8);
                outBlock = ref Unsafe.Add(ref outBlock, 8);
                remainingBlocks -= 8;
            }

            while (remainingBlocks > 0)
            {
                Vector128<byte> tmp = Sse2.Xor(inBlock, tweak);
                tmp = _dataAesCore.EncryptBlock(tmp);
                outBlock = Sse2.Xor(tmp, tweak);

                tweak = Gf128Mul(tweak, mask);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
                remainingBlocks--;
            }

            if (leftover != 0)
            {
                EncryptPartialFinalBlock(ref inBlock, ref outBlock, tweak, leftover);
            }
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int remainingBlocks = length >> 4;
            int leftover = length & 0xF;

            Debug.Assert(remainingBlocks > 0);

            if (leftover != 0) remainingBlocks--;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> mask = Vector128.Create(0x87, 1).AsByte();

            Vector128<byte> tweak = _tweakAesCore.EncryptBlock(Iv);

            while (remainingBlocks > 7)
            {
                Vector128<byte> b0 = Sse2.Xor(tweak, Unsafe.Add(ref inBlock, 0));

                Vector128<byte> tweak1 = Gf128Mul(tweak, mask);
                Vector128<byte> b1 = Sse2.Xor(tweak1, Unsafe.Add(ref inBlock, 1));

                Vector128<byte> tweak2 = Gf128Mul(tweak1, mask);
                Vector128<byte> b2 = Sse2.Xor(tweak2, Unsafe.Add(ref inBlock, 2));

                Vector128<byte> tweak3 = Gf128Mul(tweak2, mask);
                Vector128<byte> b3 = Sse2.Xor(tweak3, Unsafe.Add(ref inBlock, 3));

                Vector128<byte> tweak4 = Gf128Mul(tweak3, mask);
                Vector128<byte> b4 = Sse2.Xor(tweak4, Unsafe.Add(ref inBlock, 4));

                Vector128<byte> tweak5 = Gf128Mul(tweak4, mask);
                Vector128<byte> b5 = Sse2.Xor(tweak5, Unsafe.Add(ref inBlock, 5));

                Vector128<byte> tweak6 = Gf128Mul(tweak5, mask);
                Vector128<byte> b6 = Sse2.Xor(tweak6, Unsafe.Add(ref inBlock, 6));

                Vector128<byte> tweak7 = Gf128Mul(tweak6, mask);
                Vector128<byte> b7 = Sse2.Xor(tweak7, Unsafe.Add(ref inBlock, 7));

                _dataAesCore.DecryptBlocks8(b0, b1, b2, b3, b4, b5, b6, b7,
                    out b0, out b1, out b2, out b3, out b4, out b5, out b6, out b7);

                Unsafe.Add(ref outBlock, 0) = Sse2.Xor(tweak, b0);
                Unsafe.Add(ref outBlock, 1) = Sse2.Xor(tweak1, b1);
                Unsafe.Add(ref outBlock, 2) = Sse2.Xor(tweak2, b2);
                Unsafe.Add(ref outBlock, 3) = Sse2.Xor(tweak3, b3);
                Unsafe.Add(ref outBlock, 4) = Sse2.Xor(tweak4, b4);
                Unsafe.Add(ref outBlock, 5) = Sse2.Xor(tweak5, b5);
                Unsafe.Add(ref outBlock, 6) = Sse2.Xor(tweak6, b6);
                Unsafe.Add(ref outBlock, 7) = Sse2.Xor(tweak7, b7);

                tweak = Gf128Mul(tweak7, mask);

                inBlock = ref Unsafe.Add(ref inBlock, 8);
                outBlock = ref Unsafe.Add(ref outBlock, 8);
                remainingBlocks -= 8;
            }

            while (remainingBlocks > 0)
            {
                Vector128<byte> tmp = Sse2.Xor(inBlock, tweak);
                tmp = _dataAesCore.DecryptBlock(tmp);
                outBlock = Sse2.Xor(tmp, tweak);

                tweak = Gf128Mul(tweak, mask);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
                remainingBlocks--;
            }

            if (leftover != 0)
            {
                DecryptPartialFinalBlock(ref inBlock, ref outBlock, tweak, mask, leftover);
            }
        }

        // ReSharper disable once RedundantAssignment
        private void DecryptPartialFinalBlock(ref Vector128<byte> input, ref Vector128<byte> output,
            Vector128<byte> tweak, Vector128<byte> mask, int finalBlockLength)
        {
            Vector128<byte> finalTweak = Gf128Mul(tweak, mask);

            Vector128<byte> tmp = Sse2.Xor(input, finalTweak);
            tmp = _dataAesCore.DecryptBlock(tmp);
            output = Sse2.Xor(tmp, finalTweak);

            var x = new Buffer16();
            ref Buffer16 outBuf = ref Unsafe.As<Vector128<byte>, Buffer16>(ref output);
            Buffer16 nextInBuf = Unsafe.As<Vector128<byte>, Buffer16>(ref Unsafe.Add(ref input, 1));
            ref Buffer16 nextOutBuf = ref Unsafe.As<Vector128<byte>, Buffer16>(ref Unsafe.Add(ref output, 1));

            for (int i = 0; i < finalBlockLength; i++)
            {
                nextOutBuf[i] = outBuf[i];
                x[i] = nextInBuf[i];
            }

            for (int i = finalBlockLength; i < 16; i++)
            {
                x[i] = outBuf[i];
            }

            tmp = Sse2.Xor(x.As<Vector128<byte>>(), tweak);
            tmp = _dataAesCore.DecryptBlock(tmp);
            output = Sse2.Xor(tmp, tweak);
        }

        private void EncryptPartialFinalBlock(ref Vector128<byte> input, ref Vector128<byte> output,
            Vector128<byte> tweak, int finalBlockLength)
        {
            ref Vector128<byte> prevOutBlock = ref Unsafe.Subtract(ref output, 1);

            var x = new Buffer16();
            ref Buffer16 outBuf = ref Unsafe.As<Vector128<byte>, Buffer16>(ref output);
            Buffer16 inBuf = Unsafe.As<Vector128<byte>, Buffer16>(ref input);
            ref Buffer16 prevOutBuf = ref Unsafe.As<Vector128<byte>, Buffer16>(ref prevOutBlock);

            for (int i = 0; i < finalBlockLength; i++)
            {
                outBuf[i] = prevOutBuf[i];
                x[i] = inBuf[i];
            }

            for (int i = finalBlockLength; i < 16; i++)
            {
                x[i] = prevOutBuf[i];
            }

            Vector128<byte> tmp = Sse2.Xor(x.As<Vector128<byte>>(), tweak);
            tmp = _dataAesCore.EncryptBlock(tmp);
            prevOutBlock = Sse2.Xor(tmp, tweak);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> Gf128Mul(Vector128<byte> iv, Vector128<byte> mask)
        {
            Vector128<byte> tmp1 = Sse2.Add(iv.AsUInt64(), iv.AsUInt64()).AsByte();

            Vector128<byte> tmp2 = Sse2.Shuffle(iv.AsInt32(), 0x13).AsByte();
            tmp2 = Sse2.ShiftRightArithmetic(tmp2.AsInt32(), 31).AsByte();
            tmp2 = Sse2.And(mask, tmp2);

            return Sse2.Xor(tmp1, tmp2);
        }
    }
}
