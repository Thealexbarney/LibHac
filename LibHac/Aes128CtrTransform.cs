using System;
using System.Numerics;
using System.Security.Cryptography;

namespace LibHac
{
    public class Aes128CtrTransform
    {
        private const int BlockSize = 128;
        private const int BlockSizeBytes = BlockSize / 8;

        private readonly int _maxSize;
        public readonly byte[] Counter = new byte[BlockSizeBytes];
        private readonly byte[] _counterDec;
        private readonly byte[] _counterEnc;
        private readonly ICryptoTransform _encryptor;

        public Aes128CtrTransform(byte[] key, byte[] counter, int maxTransformSize)
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
            _maxSize = maxTransformSize;
            _counterDec = new byte[_maxSize];
            _counterEnc = new byte[_maxSize];

            Array.Copy(counter, Counter, BlockSizeBytes);
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            if (inputCount > _maxSize)
                throw new ArgumentException($"{nameof(inputCount)} cannot be greater than {_maxSize}");

            var blockCount = Util.DivideByRoundUp(inputCount, BlockSizeBytes);

            FillDecryptedCounter(blockCount);

            _encryptor.TransformBlock(_counterDec, 0, blockCount * BlockSizeBytes, _counterEnc, 0);
            XorArrays(inputBuffer, inputOffset, outputBuffer, outputOffset, _counterEnc, inputCount);

            return inputCount;
        }

        private void FillDecryptedCounter(int blockCount)
        {
            for (int i = 0; i < blockCount; i++)
            {
                Array.Copy(Counter, 0, _counterDec, i * BlockSizeBytes, BlockSizeBytes);
                IncrementCounter();
            }
        }

        private void IncrementCounter()
        {
            Util.IncrementByteArray(Counter);
        }

        private void XorArrays(byte[] inputBuffer, int inputOffset, byte[] outputBuffer, int outputOffset, byte[] xor, int length)
        {
            int i = 0;
            if (Vector.IsHardwareAccelerated)
            {
                int simdEnd = Math.Max(length - Vector<byte>.Count, 0);
                for (; i < simdEnd; i += Vector<byte>.Count)
                {
                    var inputVec = new Vector<byte>(inputBuffer, inputOffset + i);
                    var xorVec = new Vector<byte>(xor, i);
                    var outputVec = inputVec ^ xorVec;
                    outputVec.CopyTo(outputBuffer, outputOffset + i);
                }
            }

            for (; i < length; i++)
            {
                outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ xor[i]);
            }
        }
    }
}
