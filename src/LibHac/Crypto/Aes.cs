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
    }
}
