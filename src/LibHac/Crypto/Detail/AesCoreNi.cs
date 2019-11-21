#if NETCOREAPP
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using AesNi = System.Runtime.Intrinsics.X86.Aes;

namespace LibHac.Crypto.Detail
{
    [StructLayout(LayoutKind.Sequential, Size = RoundKeyCount * RoundKeySize)]
    public struct AesCoreNi
    {
        private const int RoundKeyCount = 11;
        private const int RoundKeySize = 0x10;

        private Vector128<byte> _roundKeys;

        public void Initialize(ReadOnlySpan<byte> key, bool isDecrypting)
        {
            Debug.Assert(key.Length == Aes.KeySize128);

            KeyExpansion(key, MemoryMarshal.CreateSpan(ref _roundKeys, RoundKeyCount), isDecrypting);
        }

        public readonly ReadOnlySpan<Vector128<byte>> RoundKeys =>
            MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in _roundKeys), RoundKeyCount);

        public readonly void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            for (int i = 0; i < blockCount; i++)
            {
                outBlock = EncryptBlock(inBlock);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }
        }

        public readonly void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            for (int i = 0; i < blockCount; i++)
            {
                outBlock = DecryptBlock(inBlock);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector128<byte> EncryptBlock(Vector128<byte> input)
        {
            ReadOnlySpan<Vector128<byte>> keys = RoundKeys;

            Vector128<byte> b = Sse2.Xor(input, keys[0]);
            b = AesNi.Encrypt(b, keys[1]);
            b = AesNi.Encrypt(b, keys[2]);
            b = AesNi.Encrypt(b, keys[3]);
            b = AesNi.Encrypt(b, keys[4]);
            b = AesNi.Encrypt(b, keys[5]);
            b = AesNi.Encrypt(b, keys[6]);
            b = AesNi.Encrypt(b, keys[7]);
            b = AesNi.Encrypt(b, keys[8]);
            b = AesNi.Encrypt(b, keys[9]);
            return AesNi.EncryptLast(b, keys[10]);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly Vector128<byte> DecryptBlock(Vector128<byte> input)
        {
            ReadOnlySpan<Vector128<byte>> keys = RoundKeys;

            Vector128<byte> b = Sse2.Xor(input, keys[10]);
            b = AesNi.Decrypt(b, keys[9]);
            b = AesNi.Decrypt(b, keys[8]);
            b = AesNi.Decrypt(b, keys[7]);
            b = AesNi.Decrypt(b, keys[6]);
            b = AesNi.Decrypt(b, keys[5]);
            b = AesNi.Decrypt(b, keys[4]);
            b = AesNi.Decrypt(b, keys[3]);
            b = AesNi.Decrypt(b, keys[2]);
            b = AesNi.Decrypt(b, keys[1]);
            return AesNi.DecryptLast(b, keys[0]);
        }

        public readonly void EncryptInterleaved8(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int remainingBlocks = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            while (remainingBlocks > 7)
            {
                EncryptBlocks8(
                    Unsafe.Add(ref inBlock, 0),
                    Unsafe.Add(ref inBlock, 1),
                    Unsafe.Add(ref inBlock, 2),
                    Unsafe.Add(ref inBlock, 3),
                    Unsafe.Add(ref inBlock, 4),
                    Unsafe.Add(ref inBlock, 5),
                    Unsafe.Add(ref inBlock, 6),
                    Unsafe.Add(ref inBlock, 7),
                    out Unsafe.Add(ref outBlock, 0),
                    out Unsafe.Add(ref outBlock, 1),
                    out Unsafe.Add(ref outBlock, 2),
                    out Unsafe.Add(ref outBlock, 3),
                    out Unsafe.Add(ref outBlock, 4),
                    out Unsafe.Add(ref outBlock, 5),
                    out Unsafe.Add(ref outBlock, 6),
                    out Unsafe.Add(ref outBlock, 7));

                inBlock = ref Unsafe.Add(ref inBlock, 8);
                outBlock = ref Unsafe.Add(ref outBlock, 8);
                remainingBlocks -= 8;
            }

            while (remainingBlocks > 0)
            {
                outBlock = EncryptBlock(inBlock);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
                remainingBlocks -= 1;
            }
        }

        public readonly void DecryptInterleaved8(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int remainingBlocks = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            while (remainingBlocks > 7)
            {
                DecryptBlocks8(
                    Unsafe.Add(ref inBlock, 0),
                    Unsafe.Add(ref inBlock, 1),
                    Unsafe.Add(ref inBlock, 2),
                    Unsafe.Add(ref inBlock, 3),
                    Unsafe.Add(ref inBlock, 4),
                    Unsafe.Add(ref inBlock, 5),
                    Unsafe.Add(ref inBlock, 6),
                    Unsafe.Add(ref inBlock, 7),
                    out Unsafe.Add(ref outBlock, 0),
                    out Unsafe.Add(ref outBlock, 1),
                    out Unsafe.Add(ref outBlock, 2),
                    out Unsafe.Add(ref outBlock, 3),
                    out Unsafe.Add(ref outBlock, 4),
                    out Unsafe.Add(ref outBlock, 5),
                    out Unsafe.Add(ref outBlock, 6),
                    out Unsafe.Add(ref outBlock, 7));

                inBlock = ref Unsafe.Add(ref inBlock, 8);
                outBlock = ref Unsafe.Add(ref outBlock, 8);
                remainingBlocks -= 8;
            }

            while (remainingBlocks > 0)
            {
                outBlock = DecryptBlock(inBlock);

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
                remainingBlocks -= 1;
            }
        }

        // When inlining this function, RyuJIT will almost make the
        // generated code the same as if it were manually inlined
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void EncryptBlocks8(Vector128<byte> in0,
            Vector128<byte> in1,
            Vector128<byte> in2,
            Vector128<byte> in3,
            Vector128<byte> in4,
            Vector128<byte> in5,
            Vector128<byte> in6,
            Vector128<byte> in7,
            out Vector128<byte> out0,
            out Vector128<byte> out1,
            out Vector128<byte> out2,
            out Vector128<byte> out3,
            out Vector128<byte> out4,
            out Vector128<byte> out5,
            out Vector128<byte> out6,
            out Vector128<byte> out7
        )
        {
            ReadOnlySpan<Vector128<byte>> keys = RoundKeys;

            Vector128<byte> key = keys[0];
            Vector128<byte> b0 = Sse2.Xor(in0, key);
            Vector128<byte> b1 = Sse2.Xor(in1, key);
            Vector128<byte> b2 = Sse2.Xor(in2, key);
            Vector128<byte> b3 = Sse2.Xor(in3, key);
            Vector128<byte> b4 = Sse2.Xor(in4, key);
            Vector128<byte> b5 = Sse2.Xor(in5, key);
            Vector128<byte> b6 = Sse2.Xor(in6, key);
            Vector128<byte> b7 = Sse2.Xor(in7, key);

            key = keys[1];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[2];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[3];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[4];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[5];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[6];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[7];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[8];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[9];
            b0 = AesNi.Encrypt(b0, key);
            b1 = AesNi.Encrypt(b1, key);
            b2 = AesNi.Encrypt(b2, key);
            b3 = AesNi.Encrypt(b3, key);
            b4 = AesNi.Encrypt(b4, key);
            b5 = AesNi.Encrypt(b5, key);
            b6 = AesNi.Encrypt(b6, key);
            b7 = AesNi.Encrypt(b7, key);

            key = keys[10];
            out0 = AesNi.EncryptLast(b0, key);
            out1 = AesNi.EncryptLast(b1, key);
            out2 = AesNi.EncryptLast(b2, key);
            out3 = AesNi.EncryptLast(b3, key);
            out4 = AesNi.EncryptLast(b4, key);
            out5 = AesNi.EncryptLast(b5, key);
            out6 = AesNi.EncryptLast(b6, key);
            out7 = AesNi.EncryptLast(b7, key);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly void DecryptBlocks8(
            Vector128<byte> in0,
            Vector128<byte> in1,
            Vector128<byte> in2,
            Vector128<byte> in3,
            Vector128<byte> in4,
            Vector128<byte> in5,
            Vector128<byte> in6,
            Vector128<byte> in7,
            out Vector128<byte> out0,
            out Vector128<byte> out1,
            out Vector128<byte> out2,
            out Vector128<byte> out3,
            out Vector128<byte> out4,
            out Vector128<byte> out5,
            out Vector128<byte> out6,
            out Vector128<byte> out7
            )
        {
            ReadOnlySpan<Vector128<byte>> keys = RoundKeys;

            Vector128<byte> key = keys[10];
            Vector128<byte> b0 = Sse2.Xor(in0, key);
            Vector128<byte> b1 = Sse2.Xor(in1, key);
            Vector128<byte> b2 = Sse2.Xor(in2, key);
            Vector128<byte> b3 = Sse2.Xor(in3, key);
            Vector128<byte> b4 = Sse2.Xor(in4, key);
            Vector128<byte> b5 = Sse2.Xor(in5, key);
            Vector128<byte> b6 = Sse2.Xor(in6, key);
            Vector128<byte> b7 = Sse2.Xor(in7, key);

            key = keys[9];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[8];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[7];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[6];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[5];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[4];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[3];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[2];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[1];
            b0 = AesNi.Decrypt(b0, key);
            b1 = AesNi.Decrypt(b1, key);
            b2 = AesNi.Decrypt(b2, key);
            b3 = AesNi.Decrypt(b3, key);
            b4 = AesNi.Decrypt(b4, key);
            b5 = AesNi.Decrypt(b5, key);
            b6 = AesNi.Decrypt(b6, key);
            b7 = AesNi.Decrypt(b7, key);

            key = keys[0];
            out0 = AesNi.DecryptLast(b0, key);
            out1 = AesNi.DecryptLast(b1, key);
            out2 = AesNi.DecryptLast(b2, key);
            out3 = AesNi.DecryptLast(b3, key);
            out4 = AesNi.DecryptLast(b4, key);
            out5 = AesNi.DecryptLast(b5, key);
            out6 = AesNi.DecryptLast(b6, key);
            out7 = AesNi.DecryptLast(b7, key);
        }

        private static void KeyExpansion(ReadOnlySpan<byte> key, Span<Vector128<byte>> roundKeys, bool isDecrypting)
        {
            var curKey = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(key));
            roundKeys[0] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x01));
            roundKeys[1] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x02));
            roundKeys[2] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x04));
            roundKeys[3] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x08));
            roundKeys[4] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x10));
            roundKeys[5] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x20));
            roundKeys[6] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x40));
            roundKeys[7] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x80));
            roundKeys[8] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x1b));
            roundKeys[9] = curKey;

            curKey = KeyExpansion(curKey, AesNi.KeygenAssist(curKey, 0x36));
            roundKeys[10] = curKey;

            if (isDecrypting)
            {
                for (int i = 1; i < 10; i++)
                {
                    roundKeys[i] = AesNi.InverseMixColumns(roundKeys[i]);
                }
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Vector128<byte> KeyExpansion(Vector128<byte> s, Vector128<byte> t)
        {
            t = Sse2.Shuffle(t.AsUInt32(), 0xFF).AsByte();

            s = Sse2.Xor(s, Sse2.ShiftLeftLogical128BitLane(s, 4));
            s = Sse2.Xor(s, Sse2.ShiftLeftLogical128BitLane(s, 8));

            return Sse2.Xor(s, t);
        }
    }
}
#endif
