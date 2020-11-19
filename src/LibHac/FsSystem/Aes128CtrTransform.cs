using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class Aes128CtrTransform
    {
        private const int BlockSize = 128;
        private const int BlockSizeBytes = BlockSize / 8;

        public readonly byte[] Counter = new byte[BlockSizeBytes];

        private readonly ICryptoTransform _encryptor;

        public Aes128CtrTransform(byte[] key, byte[] counter)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (counter == null) throw new ArgumentNullException(nameof(counter));
            if (key.Length != BlockSizeBytes)
                throw new ArgumentException($"{nameof(key)} must be {BlockSizeBytes} bytes long");
            if (counter.Length != BlockSizeBytes)
                throw new ArgumentException($"{nameof(counter)} must be {BlockSizeBytes} bytes long");

            var aes = Aes.Create();
            if (aes == null) throw new CryptographicException("Unable to create AES object");
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            _encryptor = aes.CreateEncryptor(key, new byte[BlockSizeBytes]);

            Array.Copy(counter, Counter, BlockSizeBytes);
        }

        public int TransformBlock(Span<byte> data)
        {
            int blockCount = BitUtil.DivideUp(data.Length, BlockSizeBytes);
            int length = blockCount * BlockSizeBytes;

            byte[] counterXor = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                Counter.CopyTo(counterXor, 0);
                FillDecryptedCounter(counterXor.AsSpan(0, length));

                _encryptor.TransformBlock(counterXor, 0, length, counterXor, 0);
                Utilities.XorArrays(data, counterXor);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(counterXor);
            }

            return data.Length;
        }

        public static void FillDecryptedCounter(Span<byte> buffer)
        {
            Span<ulong> bufL = MemoryMarshal.Cast<byte, ulong>(buffer);

            ulong hi = bufL[0];
            ulong lo = BinaryPrimitives.ReverseEndianness(bufL[1]);

            for (int i = 2; i < bufL.Length; i += 2)
            {
                lo++;
                bufL[i] = hi;
                bufL[i + 1] = BinaryPrimitives.ReverseEndianness(lo);
            }
        }
    }
}
