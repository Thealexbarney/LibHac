#if HAS_INTRINSICS
using System;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Crypto2
{
    public struct AesCbcEncryptorHw : ICipher
    {
        private AesCoreNi _aesCore;
        private Vector128<byte> _iv;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public AesCbcEncryptorHw(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _aesCore = new AesCoreNi();
            _aesCore.Initialize(key, false);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> iv = _iv;

            for (int i = 0; i < blockCount; i++)
            {
                iv = _aesCore.EncryptBlock(Sse2.Xor(iv, inBlock));

                outBlock = iv;

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }

            _iv = iv;
        }
    }

    public struct AesCbcDecryptorHw : ICipher
    {
        private AesCoreNi _aesCore;
        private Vector128<byte> _iv;

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public AesCbcDecryptorHw(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            _aesCore = new AesCoreNi();
            _aesCore.Initialize(key, true);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        [MethodImpl(MethodImplOptions.AggressiveOptimization)]
        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> iv = _iv;

            for (int i = 0; i < blockCount; i++)
            {
                Vector128<byte> decBeforeIv = _aesCore.DecryptBlock(inBlock);
                outBlock = Sse2.Xor(decBeforeIv, iv);

                iv = inBlock;

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }

            _iv = iv;
        }
    }
}
#endif