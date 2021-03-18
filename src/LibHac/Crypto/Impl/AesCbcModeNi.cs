using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Crypto.Impl
{
    public struct AesCbcModeNi
    {
        private AesCoreNi _aesCore;

        public Vector128<byte> Iv;

        public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool isDecrypting)
        {
            Debug.Assert(iv.Length == Aes.BlockSize);

            _aesCore.Initialize(key, isDecrypting);

            Iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> iv = Iv;

            for (int i = 0; i < blockCount; i++)
            {
                iv = _aesCore.EncryptBlock(Sse2.Xor(iv, inBlock));

                outBlock = iv;

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }

            Iv = iv;
        }

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int remainingBlocks = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> iv = Iv;

            while (remainingBlocks > 7)
            {
                Vector128<byte> in0 = Unsafe.Add(ref inBlock, 0);
                Vector128<byte> in1 = Unsafe.Add(ref inBlock, 1);
                Vector128<byte> in2 = Unsafe.Add(ref inBlock, 2);
                Vector128<byte> in3 = Unsafe.Add(ref inBlock, 3);
                Vector128<byte> in4 = Unsafe.Add(ref inBlock, 4);
                Vector128<byte> in5 = Unsafe.Add(ref inBlock, 5);
                Vector128<byte> in6 = Unsafe.Add(ref inBlock, 6);
                Vector128<byte> in7 = Unsafe.Add(ref inBlock, 7);

                _aesCore.DecryptBlocks8(in0, in1, in2, in3, in4, in5, in6, in7,
                    out Vector128<byte> b0,
                    out Vector128<byte> b1,
                    out Vector128<byte> b2,
                    out Vector128<byte> b3,
                    out Vector128<byte> b4,
                    out Vector128<byte> b5,
                    out Vector128<byte> b6,
                    out Vector128<byte> b7);

                Unsafe.Add(ref outBlock, 0) = Sse2.Xor(iv, b0);
                Unsafe.Add(ref outBlock, 1) = Sse2.Xor(in0, b1);
                Unsafe.Add(ref outBlock, 2) = Sse2.Xor(in1, b2);
                Unsafe.Add(ref outBlock, 3) = Sse2.Xor(in2, b3);
                Unsafe.Add(ref outBlock, 4) = Sse2.Xor(in3, b4);
                Unsafe.Add(ref outBlock, 5) = Sse2.Xor(in4, b5);
                Unsafe.Add(ref outBlock, 6) = Sse2.Xor(in5, b6);
                Unsafe.Add(ref outBlock, 7) = Sse2.Xor(in6, b7);

                iv = in7;

                inBlock = ref Unsafe.Add(ref inBlock, 8);
                outBlock = ref Unsafe.Add(ref outBlock, 8);
                remainingBlocks -= 8;
            }

            while (remainingBlocks > 0)
            {
                Vector128<byte> currentBlock = inBlock;
                Vector128<byte> decBeforeIv = _aesCore.DecryptBlock(currentBlock);
                outBlock = Sse2.Xor(decBeforeIv, iv);

                iv = currentBlock;

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
                remainingBlocks -= 1;
            }

            Iv = iv;
        }
    }
}
