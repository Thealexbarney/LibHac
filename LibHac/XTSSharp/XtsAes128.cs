﻿// Copyright (c) 2010 Gareth Lennox (garethl@dwakn.com)
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
using System.Security.Cryptography;

namespace LibHac.XTSSharp
{
    /// <summary>
    /// XTS-AES-128 implementation
    /// </summary>
    public class XtsAes128 : Xts
    {
        private const int KeyLength = 128;
        private const int KeyByteLength = KeyLength / 8;

        /// <summary>
        /// Creates a new instance
        /// </summary>
        protected XtsAes128(Func<SymmetricAlgorithm> create, byte[] key1, byte[] key2)
            : base(create, VerifyKey(KeyLength, key1), VerifyKey(KeyLength, key2))
        {
        }

        /// <summary>
        /// Creates a new implementation
        /// </summary>
        /// <param name="key1">First key</param>
        /// <param name="key2">Second key</param>
        /// <returns>Xts implementation</returns>
        /// <remarks>Keys need to be 128 bits long (i.e. 16 bytes)</remarks>
        public static Xts Create(byte[] key1, byte[] key2)
        {
            VerifyKey(KeyLength, key1);
            VerifyKey(KeyLength, key2);

            return new XtsAes128(Aes.Create, key1, key2);
        }

        /// <summary>
        /// Creates a new implementation
        /// </summary>
        /// <param name="key">Key to use</param>
        /// <returns>Xts implementation</returns>
        /// <remarks>Key need to be 256 bits long (i.e. 32 bytes)</remarks>
        public static Xts Create(byte[] key)
        {
            VerifyKey(KeyLength * 2, key);

            var key1 = new byte[KeyByteLength];
            var key2 = new byte[KeyByteLength];

            Buffer.BlockCopy(key, 0, key1, 0, KeyByteLength);
            Buffer.BlockCopy(key, KeyByteLength, key2, 0, KeyByteLength);

            return new XtsAes128(Aes.Create, key1, key2);
        }
    }
}