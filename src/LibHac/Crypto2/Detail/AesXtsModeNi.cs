#if HAS_INTRINSICS
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using LibHac.Common;

namespace LibHac.Crypto2.Detail
{
    public struct AesXtsModeNi
    {
#pragma warning disable 649
        private AesCoreNi _dataAesCore;
        private AesCoreNi _tweakAesCore;
#pragma warning restore 649

        private Vector128<byte> _iv;

        public void Initialize(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool decrypting)
        {
            Debug.Assert(iv.Length == Aes.KeySize128);

            _dataAesCore.Initialize(key1, decrypting);
            _tweakAesCore.Initialize(key2, false);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int blockCount = length >> 4;
            int leftover = length & 0xF;

            Debug.Assert(blockCount > 0);

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> mask = Vector128.Create(0x87, 1).AsByte();

            Vector128<byte> tweak = _tweakAesCore.EncryptBlock(_iv);

            for (int i = 0; i < blockCount; i++)
            {
                Vector128<byte> tmp = Sse2.Xor(inBlock, tweak);
                tmp = _dataAesCore.EncryptBlock(tmp);
                outBlock = Sse2.Xor(tmp, tweak);

                tweak = Gf128Mul(tweak, mask);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }

            if (leftover != 0)
            {
                EncryptPartialFinalBlock(ref inBlock, ref outBlock, tweak, leftover);
            }
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int length = Math.Min(input.Length, output.Length);
            int blockCount = length >> 4;
            int leftover = length & 0xF;

            Debug.Assert(blockCount > 0);

            if (leftover != 0) blockCount--;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> mask = Vector128.Create(0x87, 1).AsByte();

            Vector128<byte> tweak = _tweakAesCore.EncryptBlock(_iv);

            for (int i = 0; i < blockCount; i++)
            {
                Vector128<byte> tmp = Sse2.Xor(inBlock, tweak);
                tmp = _dataAesCore.DecryptBlock(tmp);
                outBlock = Sse2.Xor(tmp, tweak);

                tweak = Gf128Mul(tweak, mask);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
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
            ref Buffer16 nextInBuf = ref Unsafe.As<Vector128<byte>, Buffer16>(ref Unsafe.Add(ref input, 1));
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
            ref Buffer16 inBuf = ref Unsafe.As<Vector128<byte>, Buffer16>(ref input);
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
#endif
