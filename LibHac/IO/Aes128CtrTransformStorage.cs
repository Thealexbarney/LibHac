using System;
using System.Buffers;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace LibHac.IO
{
    public class Aes128CtrTransformStorage
    {
        private const int BlockSize = 128;
        private const int BlockSizeBytes = BlockSize / 8;

        public readonly byte[] Counter = new byte[BlockSizeBytes];

        private readonly ICryptoTransform _encryptor;

        public Aes128CtrTransformStorage(byte[] key, byte[] counter)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (counter == null) throw new ArgumentNullException(nameof(counter));
            if (key.Length != BlockSizeBytes)
                throw new ArgumentException($"{nameof(key)} must be {BlockSizeBytes} bytes long");
            if (counter.Length != BlockSizeBytes)
                throw new ArgumentException($"{nameof(counter)} must be {BlockSizeBytes} bytes long");

            Aes aes = Aes.Create();
            if (aes == null) throw new CryptographicException("Unable to create AES object");
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            _encryptor = aes.CreateEncryptor(key, new byte[BlockSizeBytes]);

            Array.Copy(counter, Counter, BlockSizeBytes);
        }

        public int TransformBlock(Span<byte> data)
        {
            int blockCount = Util.DivideByRoundUp(data.Length, BlockSizeBytes);
            int length = blockCount * BlockSizeBytes;

            byte[] counterXor = ArrayPool<byte>.Shared.Rent(length);
            try
            {
                FillDecryptedCounter(blockCount, counterXor);
                _encryptor.TransformBlock(counterXor, 0, length, counterXor, 0);

                XorArrays(data, counterXor);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(counterXor);
            }

            return data.Length;
        }

        private void FillDecryptedCounter(int blockCount, byte[] buffer)
        {
            for (int i = 0; i < blockCount; i++)
            {
                Array.Copy(Counter, 0, buffer, i * BlockSizeBytes, BlockSizeBytes);
                IncrementCounter();
            }
        }

        private void IncrementCounter()
        {
            Util.IncrementByteArray(Counter);
        }

        private void XorArrays(Span<byte> transformData, Span<byte> xorData)
        {
            int sisdStart = 0;
            if (Vector.IsHardwareAccelerated)
            {
                Span<Vector<byte>> dataVec = MemoryMarshal.Cast<byte, Vector<byte>>(transformData);
                Span<Vector<byte>> xorVec = MemoryMarshal.Cast<byte, Vector<byte>>(xorData);
                sisdStart = dataVec.Length * Vector<byte>.Count;

                for (int i = 0; i < dataVec.Length; i++)
                {
                    dataVec[i] ^= xorVec[i];
                }
            }

            for (int i = sisdStart; i < transformData.Length; i++)
            {
                transformData[i] ^= xorData[i];
            }
        }
    }
}
