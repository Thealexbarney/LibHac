// ReSharper disable AssignmentIsFullyDiscarded
using System;

#if HAS_INTRINSICS
using LibHac.Crypto.Detail;

using AesNi = System.Runtime.Intrinsics.X86.Aes;
#endif

namespace LibHac.Crypto
{
    public static class Aes
    {
        public const int KeySize128 = 0x10;
        public const int BlockSize = 0x10;

        public static bool IsAesNiSupported()
        {
#if HAS_INTRINSICS
            return AesNi.IsSupported;
#else
            return false;
#endif
        }

        public static ICipher CreateEcbDecryptor(ReadOnlySpan<byte> key, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesEcbDecryptorNi(key);
            }
#endif
            return new AesEcbDecryptor(key);
        }

        public static ICipher CreateEcbEncryptor(ReadOnlySpan<byte> key, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesEcbEncryptorNi(key);
            }
#endif
            return new AesEcbEncryptor(key);
        }

        public static ICipher CreateCbcDecryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCbcDecryptorNi(key, iv);
            }
#endif
            return new AesCbcDecryptor(key, iv);
        }

        public static ICipher CreateCbcEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCbcEncryptorNi(key, iv);
            }
#endif
            return new AesCbcEncryptor(key, iv);
        }

        public static ICipherWithIv CreateCtrDecryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCtrCipherNi(key, iv);
            }
#endif
            // Encryption and decryption in counter mode is the same operation
            return CreateCtrEncryptor(key, iv, preferDotNetCrypto);
        }

        public static ICipherWithIv CreateCtrEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCtrCipherNi(key, iv);
            }
#endif
            return new AesCtrCipher(key, iv);
        }

        public static ICipherWithIv CreateXtsDecryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesXtsDecryptorNi(key1, key2, iv);
            }
#endif
            return new AesXtsDecryptor(key1, key2, iv);
        }

        public static ICipherWithIv CreateXtsEncryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesXtsEncryptorNi(key1, key2, iv);
            }
#endif
            return new AesXtsEncryptor(key1, key2, iv);
        }

        public static void EncryptEcb128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesEcbModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key, false);
                cipherNi.Encrypt(input, output);
                return;
            }
#endif
            ICipher cipher = CreateEcbEncryptor(key, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptEcb128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesEcbModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key, true);
                cipherNi.Decrypt(input, output);
                return;
            }
#endif
            ICipher cipher = CreateEcbDecryptor(key, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptCbc128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesCbcModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key, iv, false);
                cipherNi.Encrypt(input, output);
                return;
            }
#endif
            ICipher cipher = CreateCbcEncryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptCbc128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesCbcModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key, iv, true);
                cipherNi.Decrypt(input, output);
                return;
            }
#endif
            ICipher cipher = CreateCbcDecryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptCtr128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesCtrModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key, iv);
                cipherNi.Transform(input, output);
                return;
            }
#endif
            ICipher cipher = CreateCtrEncryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptCtr128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesCtrModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key, iv);
                cipherNi.Transform(input, output);
                return;
            }
#endif
            ICipher cipher = CreateCtrDecryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptXts128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key1,
            ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesXtsModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key1, key2, iv, false);
                cipherNi.Encrypt(input, output);
                return;
            }
#endif
            ICipher cipher = CreateXtsEncryptor(key1, key2, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptXts128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key1,
            ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                AesXtsModeNi cipherNi;
                unsafe { _ = &cipherNi; } // workaround for CS0165

                cipherNi.Initialize(key1, key2, iv, true);
                cipherNi.Decrypt(input, output);
                return;
            }
#endif
            ICipher cipher = CreateXtsDecryptor(key1, key2, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        /**
         * <param name="k">A byte span containing the 128-bit key used in the AES-CBC-128 steps</param>
         * <param name="m">A byte span containing the message to be authenticated</param>
         * <param name="mIndex">The offset within the byte span at which the message will be read from</param>
         * <param name="t">A byte span to output the message authentication code into</param>
         * <param name="tIndex">The offset within the byte span at which the authentication code will be written to</param>
         * <param name="len">The length of the message</param>
         * <remarks>https://tools.ietf.org/html/rfc4493</remarks>
         */
        public static void CalculateCmac(Span<byte> k, Span<byte> m, int mIndex, Span<byte> t, int tIndex, int len)
        {
            ReadOnlySpan<byte> zero = stackalloc byte[16];

            // Step 1, AES-128 with key K is applied to an all-zero input block.
            Span<byte> l = stackalloc byte[16];

            EncryptCbc128(zero, l, k, zero);

            // Step 2, K1 is derived through the following operation:
            Span<byte> k1 = LeftShiftBytes(l);
            if ((l[0] & 0x80) == 0x80) // If the most significant bit of L is equal to 0, K1 is the left-shift of L by 1 bit.
                k1[15] ^= 0x87;        // Otherwise, K1 is the XOR of const_Rb and the left-shift of L by 1 bit.

            // Step 3, K2 is derived through the following operation:
            Span<byte> k2 = LeftShiftBytes(k1);
            if ((k1[0] & 0x80) == 0x80) // If the most significant bit of K1 is equal to 0, K2 is the left-shift of K1 by 1 bit.
                k2[15] ^= 0x87;         // Otherwise, K2 is the XOR of const_Rb and the left-shift of K1 by 1 bit.

            if (len != 0 && len % 16 == 0) // If the size of the input message block is equal to a positive multiple of the block size (namely, 128 bits),
            {                              // the last block shall be XOR'ed with K1 before processing
                Span<byte> message = stackalloc byte[len];
                m.Slice(mIndex, len).CopyTo(message);

                for (int j = 0; j < k1.Length; j++)
                    message[message.Length - 16 + j] ^= k1[j];

                Span<byte> encResult = stackalloc byte[message.Length];
                EncryptCbc128(message, encResult, k, zero); // The result of the previous process will be the input of the last encryption.
                encResult.Slice(message.Length - 0x10).CopyTo(t.Slice(tIndex));
            }
            else // Otherwise, the last block shall be padded with 10^i and XOR'ed with K2.
            {
                Span<byte> message = stackalloc byte[len + (16 - len % 16)];
                message[len] = 0x80;
                m.Slice(mIndex, len).CopyTo(message);

                for (int j = 0; j < k2.Length; j++)
                    message[message.Length - 16 + j] ^= k2[j];

                Span<byte> encResult = stackalloc byte[message.Length];
                EncryptCbc128(message, encResult, k, zero); // The result of the previous process will be the input of the last encryption.
                encResult.Slice(message.Length - 0x10).CopyTo(t.Slice(tIndex));
            }
        }

        /**
         * <param name="k">A byte span containing the 128-bit key used in the AES-CBC-128 steps</param>
         * <param name="m">A byte span containing the message to be authenticated</param>
         * <param name="t">A byte span to output the message authentication code into</param>
         * <remarks>https://tools.ietf.org/html/rfc4493</remarks>
         */
        public static void CalculateCmac(Span<byte> k, Span<byte> m, Span<byte> t) =>
            CalculateCmac(k, m, 0, t, 0, m.Length);

        private static byte[] LeftShiftBytes(Span<byte> bytes)
        {
            var shifted = new byte[bytes.Length];
            byte carry = 0;

            for (var i = bytes.Length - 1; i >= 0; i--)
            {
                var b = (ushort)(bytes[i] << 1);
                shifted[i] = (byte)((b & 0xff) + carry);
                carry = (byte)((b & 0xff00) >> 8);
            }

            return shifted;
        }
    }
}
