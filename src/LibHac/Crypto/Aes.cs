// ReSharper disable AssignmentIsFullyDiscarded
using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Crypto.Impl;
using LibHac.Diag;

using AesNi = System.Runtime.Intrinsics.X86.Aes;

namespace LibHac.Crypto
{
    public static class Aes
    {
        public const int KeySize128 = 0x10;
        public const int BlockSize = 0x10;

        public static bool IsAesNiSupported()
        {
            return AesNi.IsSupported;
        }

        public static ICipher CreateEcbDecryptor(ReadOnlySpan<byte> key, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesEcbDecryptorNi(key);
            }

            return new AesEcbDecryptor(key);
        }

        public static ICipher CreateEcbEncryptor(ReadOnlySpan<byte> key, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesEcbEncryptorNi(key);
            }

            return new AesEcbEncryptor(key);
        }

        public static ICipher CreateCbcDecryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCbcDecryptorNi(key, iv);
            }

            return new AesCbcDecryptor(key, iv);
        }

        public static ICipher CreateCbcEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCbcEncryptorNi(key, iv);
            }

            return new AesCbcEncryptor(key, iv);
        }

        public static ICipherWithIv CreateCtrDecryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCtrCipherNi(key, iv);
            }

            // Encryption and decryption in counter mode is the same operation
            return CreateCtrEncryptor(key, iv, preferDotNetCrypto);
        }

        public static ICipherWithIv CreateCtrEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCtrCipherNi(key, iv);
            }

            return new AesCtrCipher(key, iv);
        }

        public static ICipherWithIv CreateXtsDecryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesXtsDecryptorNi(key1, key2, iv);
            }

            return new AesXtsDecryptor(key1, key2, iv);
        }

        public static ICipherWithIv CreateXtsEncryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesXtsEncryptorNi(key1, key2, iv);
            }

            return new AesXtsEncryptor(key1, key2, iv);
        }

        public static void EncryptEcb128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesEcbModeNi cipherNi);

                cipherNi.Initialize(key, false);
                cipherNi.Encrypt(input, output);
                return;
            }

            ICipher cipher = CreateEcbEncryptor(key, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptEcb128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesEcbModeNi cipherNi);

                cipherNi.Initialize(key, true);
                cipherNi.Decrypt(input, output);
                return;
            }

            ICipher cipher = CreateEcbDecryptor(key, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptCbc128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesCbcModeNi cipherNi);

                cipherNi.Initialize(key, iv, false);
                cipherNi.Encrypt(input, output);
                return;
            }

            ICipher cipher = CreateCbcEncryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptCbc128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesCbcModeNi cipherNi);

                cipherNi.Initialize(key, iv, true);
                cipherNi.Decrypt(input, output);
                return;
            }

            ICipher cipher = CreateCbcDecryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptCtr128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesCtrModeNi cipherNi);

                cipherNi.Initialize(key, iv);
                cipherNi.Transform(input, output);
                return;
            }

            ICipher cipher = CreateCtrEncryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptCtr128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesCtrModeNi cipherNi);

                cipherNi.Initialize(key, iv);
                cipherNi.Transform(input, output);
                return;
            }

            ICipher cipher = CreateCtrDecryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptXts128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key1,
            ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesXtsModeNi cipherNi);

                cipherNi.Initialize(key1, key2, iv, false);
                cipherNi.Encrypt(input, output);
                return;
            }

            ICipher cipher = CreateXtsEncryptor(key1, key2, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptXts128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key1,
            ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                Unsafe.SkipInit(out AesXtsModeNi cipherNi);

                cipherNi.Initialize(key1, key2, iv, true);
                cipherNi.Decrypt(input, output);
                return;
            }

            ICipher cipher = CreateXtsDecryptor(key1, key2, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        /// <summary>
        /// Computes the CMAC of the provided data using AES-128.
        /// </summary>
        /// <param name="mac">The buffer where the generated MAC will be placed. Must be at least 16 bytes long.</param>
        /// <param name="data">The message on which the MAC will be calculated.</param>
        /// <param name="key">The 128-bit AES key used to calculate the MAC.</param>
        /// <remarks>https://tools.ietf.org/html/rfc4493</remarks>
        public static void CalculateCmac(Span<byte> mac, ReadOnlySpan<byte> data, ReadOnlySpan<byte> key)
        {
            ReadOnlySpan<byte> zero = new Buffer16();
            int len = data.Length;

            // Step 1, AES-128 with key K is applied to an all-zero input block.
            Span<byte> l = stackalloc byte[16];
            EncryptCbc128(zero, l, key, zero);

            // Step 2, K1 is derived through the following operation:
            Span<byte> k1 = stackalloc byte[16];
            LeftShiftBytes(l, k1);
            if ((l[0] & 0x80) == 0x80) // If the most significant bit of L is equal to 0, K1 is the left-shift of L by 1 bit.
                k1[15] ^= 0x87;        // Otherwise, K1 is the XOR of const_Rb and the left-shift of L by 1 bit.

            // Step 3, K2 is derived through the following operation:
            Span<byte> k2 = stackalloc byte[16];
            LeftShiftBytes(k1, k2);
            if ((k1[0] & 0x80) == 0x80) // If the most significant bit of K1 is equal to 0, K2 is the left-shift of K1 by 1 bit.
                k2[15] ^= 0x87;        // Otherwise, K2 is the XOR of const_Rb and the left-shift of K1 by 1 bit.

            // ReSharper disable once RedundantAssignment
            Span<byte> paddedMessage = l;

            if (len != 0 && len % 16 == 0) // If the size of the input message block is equal to a positive multiple of the block size (namely, 128 bits),
            {                              // the last block shall be XOR'ed with K1 before processing
                paddedMessage = len < 0x800 ? stackalloc byte[len] : new byte[len];
                data.CopyTo(paddedMessage);

                for (int j = 0; j < k1.Length; j++)
                    paddedMessage[paddedMessage.Length - 16 + j] ^= k1[j];
            }
            else // Otherwise, the last block shall be padded with 10^i and XOR'ed with K2.
            {
                int paddedLength = len + (16 - len % 16);
                paddedMessage = paddedLength < 0x800 ? stackalloc byte[paddedLength] : new byte[paddedLength];

                paddedMessage.Slice(len).Clear();
                paddedMessage[len] = 0x80;
                data.CopyTo(paddedMessage);

                for (int j = 0; j < k2.Length; j++)
                    paddedMessage[paddedMessage.Length - 16 + j] ^= k2[j];
            }

            EncryptCbc128(paddedMessage, paddedMessage, key, zero); // The result of the previous process will be the input of the last encryption.
            paddedMessage.Slice(paddedMessage.Length - 0x10).CopyTo(mac);
        }

        private static void LeftShiftBytes(ReadOnlySpan<byte> input, Span<byte> output)
        {
            Assert.SdkRequiresGreaterEqual(output.Length, input.Length);

            byte carry = 0;

            for (int i = input.Length - 1; i >= 0; i--)
            {
                ushort b = (ushort)(input[i] << 1);
                output[i] = (byte)((b & 0xff) + carry);
                carry = (byte)((b & 0xff00) >> 8);
            }
        }
    }
}
