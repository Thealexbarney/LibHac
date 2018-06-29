// The MIT License (MIT)

// Copyright (c) 2014 Hans Wolff

// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:

// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.

// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Security.Cryptography;

namespace libhac
{
    public class CounterModeCryptoTransform
    {
        private const int BlockSize = 128;
        private const int BlockSizeBytes = BlockSize / 8;
        private readonly byte[] _counter;
        private readonly byte[] _counterEnc = new byte[0x10];
        private readonly ICryptoTransform _counterEncryptor;

        public CounterModeCryptoTransform(SymmetricAlgorithm symmetricAlgorithm, byte[] key, byte[] counter)
        {
            if (key == null) throw new ArgumentNullException(nameof(key));
            if (counter == null) throw new ArgumentNullException(nameof(counter));
            if (counter.Length != BlockSizeBytes)
                throw new ArgumentException(String.Format("Counter size must be same as block size (actual: {0}, expected: {1})",
                    counter.Length, BlockSizeBytes));

            _counter = counter;
            _counterEncryptor = symmetricAlgorithm.CreateEncryptor(key, new byte[BlockSize / 8]);
        }

        public int TransformBlock(byte[] inputBuffer, int inputOffset, byte[] outputBuffer, int outputOffset)
        {
            EncryptCounterThenIncrement();
            for (int i = 0; i < 16; i++)
            {
                outputBuffer[outputOffset + i] = (byte)(inputBuffer[inputOffset + i] ^ _counterEnc[i]);
            }

            return 16;
        }

        public void UpdateCounter(long offset)
        {
            offset >>= 4;
            for (uint j = 0; j < 0x8; j++)
            {
                _counter[0x10 - j - 1] = (byte)(offset & 0xFF);
                offset >>= 8;
            }
        }

        private void EncryptCounterThenIncrement()
        {
            _counterEncryptor.TransformBlock(_counter, 0, _counter.Length, _counterEnc, 0);
            IncrementCounter();
        }

        private void IncrementCounter()
        {
            for (var i = _counter.Length - 1; i >= 0; i--)
            {
                if (++_counter[i] != 0)
                    break;
            }
        }

        public void Dispose()
        {
        }
    }
}