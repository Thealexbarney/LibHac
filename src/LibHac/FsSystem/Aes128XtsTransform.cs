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
using System.Buffers;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class Aes128XtsTransform
    {
        private const int BlockSize = 128;
        private const int BlockSizeBytes = BlockSize / 8;

        private readonly byte[] _cc = new byte[16];
        private readonly bool _decrypting;
        private readonly ICryptoTransform _key1;
        private readonly ICryptoTransform _key2;

        private readonly byte[] _pp = new byte[16];
        private readonly byte[] _t = new byte[16];

        public Aes128XtsTransform(byte[] key1, byte[] key2, bool decrypting)
        {
            if (key1?.Length != BlockSizeBytes || key2?.Length != BlockSizeBytes)
                throw new ArgumentException($"Each key must be {BlockSizeBytes} bytes long");

            var aes = Aes.Create();
            if (aes == null) throw new CryptographicException("Unable to create AES object");
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.None;

            _decrypting = decrypting;

            if (decrypting)
            {
                _key1 = aes.CreateDecryptor(key1, new byte[BlockSizeBytes]);
                _key2 = aes.CreateEncryptor(key2, new byte[BlockSizeBytes]);
            }
            else
            {
                _key1 = aes.CreateEncryptor(key1, new byte[BlockSizeBytes]);
                _key2 = aes.CreateEncryptor(key2, new byte[BlockSizeBytes]);
            }
        }

        /// <summary>
        /// Transforms a single block.
        /// </summary>
        /// <param name="buffer"> The input for which to compute the transform.</param>
        /// <param name="offset">The offset into the byte array from which to begin using data.</param>
        /// <param name="count">The number of bytes in the byte array to use as data.</param>
        /// <param name="sector">The sector number of the block</param>
        /// <returns>The number of bytes written.</returns>
        public int TransformBlock(byte[] buffer, int offset, int count, ulong sector)
        {
            int lim;

            /* get number of blocks */
            int m = count >> 4;
            int mo = count & 15;
            int alignedCount = Alignment.AlignUp(count, BlockSizeBytes);

            /* for i = 0 to m-2 do */
            if (mo == 0)
                lim = m;
            else
                lim = m - 1;

            byte[] tweak = ArrayPool<byte>.Shared.Rent(alignedCount);
            try
            {
                FillArrayFromSector(tweak, sector);

                /* encrypt the tweak */
                _key2.TransformBlock(tweak, 0, 16, tweak, 0);

                FillTweakBuffer(tweak.AsSpan(0, alignedCount));

                if (lim > 0)
                {
                    Utilities.XorArrays(buffer.AsSpan(offset, lim * 16), tweak);
                    _key1.TransformBlock(buffer, offset, lim * 16, buffer, offset);
                    Utilities.XorArrays(buffer.AsSpan(offset, lim * 16), tweak);
                }

                if (mo > 0)
                {
                    Buffer.BlockCopy(tweak, lim * 16, _t, 0, 16);

                    if (_decrypting)
                    {
                        Buffer.BlockCopy(tweak, lim * 16 + 16, _cc, 0, 16);

                        /* CC = tweak encrypt block m-1 */
                        TweakCrypt(buffer, offset, _pp, 0, _cc);

                        /* Cm = first ptlen % 16 bytes of CC */
                        int i;
                        for (i = 0; i < mo; i++)
                        {
                            _cc[i] = buffer[16 + i + offset];
                            buffer[16 + i + offset] = _pp[i];
                        }

                        for (; i < 16; i++)
                        {
                            _cc[i] = _pp[i];
                        }

                        /* Cm-1 = Tweak encrypt PP */
                        TweakCrypt(_cc, 0, buffer, offset, _t);
                    }
                    else
                    {
                        /* CC = tweak encrypt block m-1 */
                        TweakCrypt(buffer, offset, _cc, 0, _t);

                        /* Cm = first ptlen % 16 bytes of CC */
                        int i;
                        for (i = 0; i < mo; i++)
                        {
                            _pp[i] = buffer[16 + i + offset];
                            buffer[16 + i + offset] = _cc[i];
                        }

                        for (; i < 16; i++)
                        {
                            _pp[i] = _cc[i];
                        }

                        /* Cm-1 = Tweak encrypt PP */
                        TweakCrypt(_pp, 0, buffer, offset, _t);
                    }
                }
            }
            finally { ArrayPool<byte>.Shared.Return(tweak); }

            return count;
        }

        private static void FillTweakBuffer(Span<byte> buffer)
        {
            Span<ulong> bufL = MemoryMarshal.Cast<byte, ulong>(buffer);

            ulong a = bufL[1];
            ulong b = bufL[0];

            for (int i = 2; i < bufL.Length; i += 2)
            {
                ulong tt = (ulong)((long)a >> 63) & 0x87;

                a = (a << 1) | (b >> 63);
                b = (b << 1) ^ tt;

                bufL[i + 1] = a;
                bufL[i] = b;
            }
        }

        /// <summary>
        /// Fills a byte array from a sector number (little endian)
        /// </summary>
        /// <param name="value">The destination</param>
        /// <param name="sector">The sector number</param>
        private static void FillArrayFromSector(byte[] value, ulong sector)
        {
            for (int i = 0xF; i >= 0; i--)
            {
                value[i] = (byte)sector;
                sector >>= 8;
            }
        }

        /// <summary>
        /// Performs the Xts TweakCrypt operation
        /// </summary>
        private void TweakCrypt(byte[] inputBuffer, int inputOffset, byte[] outputBuffer, int outputOffset, byte[] t)
        {
            for (int x = 0; x < 16; x++)
            {
                outputBuffer[x + outputOffset] = (byte)(inputBuffer[x + inputOffset] ^ t[x]);
            }

            _key1.TransformBlock(outputBuffer, outputOffset, 16, outputBuffer, outputOffset);

            for (int x = 0; x < 16; x++)
            {
                outputBuffer[x + outputOffset] = (byte)(outputBuffer[x + outputOffset] ^ t[x]);
            }

            MultiplyByX(t);
        }

        /// <summary>
        /// Multiply by x
        /// </summary>
        /// <param name="i">The value to multiply by x (LFSR shift)</param>
        private static void MultiplyByX(byte[] i)
        {
            byte t = 0, tt = 0;

            for (int x = 0; x < 16; x++)
            {
                tt = (byte)(i[x] >> 7);
                i[x] = (byte)(((i[x] << 1) | t) & 0xFF);
                t = tt;
            }

            if (tt > 0)
                i[0] ^= 0x87;
        }
    }
}
