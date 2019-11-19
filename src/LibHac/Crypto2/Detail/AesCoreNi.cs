#if NETCOREAPP
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

using AesNi = System.Runtime.Intrinsics.X86.Aes;

namespace LibHac.Crypto2.Detail
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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
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
