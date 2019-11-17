using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LibHac.Crypto2
{
    public class AesCtrEncryptor : ICipher
    {
        private const int BlockSize = 128;
        private const int BlockSizeBytes = BlockSize / 8;

        private readonly ICryptoTransform _encryptor;
        private readonly byte[] _counter = new byte[0x10];

        public AesCtrEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv)
        {
            Aes aes = Aes.Create();
            if (aes == null) throw new CryptographicException("Unable to create AES object");

            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            _encryptor = aes.CreateEncryptor(key.ToArray(), new byte[0x10]);

            iv.CopyTo(_counter);
        }

        public void Transform(ReadOnlySpan<byte> input, Span<byte> output)
        {
            int blockCount = Util.DivideByRoundUp(input.Length, BlockSizeBytes);
            int length = blockCount * BlockSizeBytes;

            byte[] counterXor = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                FillDecryptedCounter(_counter, counterXor.AsSpan(0, length));

                _encryptor.TransformBlock(counterXor, 0, length, counterXor, 0);

                input.CopyTo(output);
                Util.XorArrays(output, counterXor);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(counterXor);
            }
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
