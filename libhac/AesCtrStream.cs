// Copyright (c) 2010 Gareth Lennox (garethl@dwakn.com)
// All rights reserved.

// Redistribution and use in source and binary forms, with or without modification,
// are permitted provided that the following conditions are met:

//     * Redistributions of source code must retain the above copyright notice,
//       this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright notice,
//       this list of conditions and the following disclaimer in the documentation
//       and/or other materials provided with the distribution.
//     * Neither the name of Gareth Lennox nor the names of its
//       contributors may be used to endorse or promote products derived from this
//       software without specific prior written permission.

// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE
// FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
// DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
// SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
// CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
// OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF
// THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using libhac.XTSSharp;

namespace libhac
{
    /// <summary>
    /// Xts sector-based
    /// </summary>
    public class AesCtrStream : SectorStream
    {
        /// <summary>
        /// The default sector size
        /// </summary>
        public const int DefaultSectorSize = 16;

        private readonly byte[] _initialCounter;
        private readonly long _counterOffset;
        private readonly byte[] _tempBuffer;
        private readonly Aes _aes;
        protected CounterModeCryptoTransform Decryptor;

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream</param>
        /// <param name="key">The decryption key</param>
        /// <param name="counterOffset">Offset to add to the counter</param>
        public AesCtrStream(Stream baseStream, byte[] key, long counterOffset = 0)
            : this(baseStream, key, 0, baseStream.Length, counterOffset) { }

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream</param>
        /// <param name="key">The decryption key</param>
        /// <param name="counter">The intial counter</param>
        public AesCtrStream(Stream baseStream, byte[] key, byte[] counter)
            : base(baseStream, 0x10, 0)
        {
            _initialCounter = counter.ToArray();
            _counterOffset = 0;
            Length = baseStream.Length;
            _tempBuffer = new byte[0x10];

            _aes = Aes.Create();
            if (_aes == null) throw new CryptographicException("Unable to create AES object");
            _aes.Key = key;
            _aes.Mode = CipherMode.ECB;
            _aes.Padding = PaddingMode.None;
            Decryptor = new CounterModeCryptoTransform(_aes, _aes.Key, _initialCounter ?? new byte[0x10]);
        }

        /// <summary>
        /// Creates a new stream
        /// </summary>
        /// <param name="baseStream">The base stream</param>
        /// <param name="key">The decryption key</param>
        /// <param name="offset">Offset to start at in the input stream</param>
        /// <param name="length">The length of the created stream</param>
        /// <param name="counterOffset">Offset to add to the counter</param>
        /// <param name="ctrHi">The value of the upper 64 bits of the counter</param>
        public AesCtrStream(Stream baseStream, byte[] key, long offset, long length, long counterOffset, byte[] ctrHi = null)
            : base(baseStream, 0x10, offset)
        {
            _initialCounter = new byte[0x10];
            if (ctrHi != null)
            {
                Array.Copy(ctrHi, _initialCounter, 8);
            }

            _counterOffset = counterOffset;
            Length = length;
            _tempBuffer = new byte[0x10];

            _aes = Aes.Create();
            if (_aes == null) throw new CryptographicException("Unable to create AES object");
            _aes.Key = key;
            _aes.Mode = CipherMode.ECB;
            _aes.Padding = PaddingMode.None;
            Decryptor = CreateDecryptor();

        }

        private CounterModeCryptoTransform CreateDecryptor()
        {
            var dec = new CounterModeCryptoTransform(_aes, _aes.Key, _initialCounter ?? new byte[0x10]);
            dec.UpdateCounter(_counterOffset + Position);
            return dec;
        }

        /// <summary>
        /// Releases the unmanaged resources used by the <see cref="T:System.IO.Stream"/> and optionally releases the managed resources.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            Decryptor?.Dispose();
        }

        public override void Flush()
        {
            throw new NotImplementedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotImplementedException();
        }

        public override void SetLength(long value)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Writes a sequence of bytes to the current stream and advances the current position within this stream by the number of bytes written.
        /// </summary>
        /// <param name="buffer">An array of bytes. This method copies <paramref name="count"/> bytes from <paramref name="buffer"/> to the current stream.</param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin copying bytes to the current stream.</param>
        /// <param name="count">The number of bytes to be written to the current stream.</param>
        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotImplementedException();
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length { get; }

        public override long Position
        {
            get => base.Position;
            set
            {
                base.Position = value;
                Decryptor.UpdateCounter(_counterOffset + base.Position);
            }
        }

        /// <summary>
        /// Reads a sequence of bytes from the current stream and advances the position within the stream by the number of bytes read.
        /// </summary>
        /// <returns>The total number of bytes read into the buffer. This can be less than the number of bytes requested if that many bytes are not currently available, or zero (0) if the end of the stream has been reached.</returns>
        /// <param name="buffer">An array of bytes. When this method returns, the buffer contains the specified byte array with the values between <paramref name="offset"/> and (<paramref name="offset"/> + <paramref name="count"/> - 1) replaced by the bytes read from the current source. </param>
        /// <param name="offset">The zero-based byte offset in <paramref name="buffer"/> at which to begin storing the data read from the current stream.</param>
        /// <param name="count">The maximum number of bytes to be read from the current stream.</param>
        public override int Read(byte[] buffer, int offset, int count)
        {
            ValidateSize(count);

            //read the sector from the base stream
            var ret = base.Read(_tempBuffer, 0, count);

            if (ret == 0)
                return 0;

            if (Decryptor == null)
                Decryptor = CreateDecryptor();

            //decrypt the sector
            var retV = Decryptor.TransformBlock(_tempBuffer, 0, buffer, offset);

            //Console.WriteLine("Decrypting sector {0} == {1} bytes", currentSector, retV);

            return retV;
        }
    }
}