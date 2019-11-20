#if HAS_INTRINSICS
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;

namespace LibHac.Crypto.Detail
{
    public struct AesCbcModeNi
    {
#pragma warning disable 649
        private AesCoreNi _aesCore;
#pragma warning restore 649

        private Vector128<byte> _iv;

        public void Initialize(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool isDecrypting)
        {
            Debug.Assert(iv.Length == Aes.BlockSize);

            _aesCore.Initialize(key, isDecrypting);

            _iv = Unsafe.ReadUnaligned<Vector128<byte>>(ref MemoryMarshal.GetReference(iv));
        }

        public void Encrypt(ReadOnlySpan<byte> input, Span<byte> output)
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

        public void Decrypt(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Math.Min(input.Length, output.Length) >> 4;

            ref Vector128<byte> inBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(input));
            ref Vector128<byte> outBlock = ref Unsafe.As<byte, Vector128<byte>>(ref MemoryMarshal.GetReference(output));

            Vector128<byte> iv = _iv;

            for (int i = 0; i < blockCount; i++)
            {
                Vector128<byte> currentBlock = inBlock;
                Vector128<byte> decBeforeIv = _aesCore.DecryptBlock(currentBlock);
                outBlock = Sse2.Xor(decBeforeIv, iv);

                iv = currentBlock;

                inBlock = ref Unsafe.Add(ref inBlock, 1);
                outBlock = ref Unsafe.Add(ref outBlock, 1);
            }

            _iv = iv;
        }
    }
}
#endif
