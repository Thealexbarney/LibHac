using System;

#if HAS_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif

namespace LibHac.Crypto2
{
    public static class AesCrypto
    {
        public const int KeySize128 = 0x10;
        public const int BlockSize = 0x10;

        public static bool IsAesNiSupported()
        {
#if HAS_INTRINSICS
            return Aes.IsSupported;
#else
            return false;
#endif
        }

        public static ICipher CreateEcbDecryptor(ReadOnlySpan<byte> key, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesEcbDecryptorHw(key);
            }
#endif
            return new AesEcbDecryptor(key);
        }

        public static ICipher CreateEcbEncryptor(ReadOnlySpan<byte> key, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesEcbEncryptorHw(key);
            }
#endif
            return new AesEcbEncryptor(key);
        }

        public static ICipher CreateCbcDecryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCbcDecryptorHw(key, iv);
            }
#endif
            return new AesCbcDecryptor(key, iv);
        }

        public static ICipher CreateCbcEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCbcEncryptorHw(key, iv);
            }
#endif
            return new AesCbcEncryptor(key, iv);
        }

        public static ICipher CreateCtrDecryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            // Encryption and decryption in counter mode is the same operation
            return CreateCtrEncryptor(key, iv, preferDotNetCrypto);
        }

        public static ICipher CreateCtrEncryptor(ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesCtrEncryptorHw(key, iv);
            }
#endif
            return new AesCtrEncryptor(key, iv);
        }

        public static ICipher CreateXtsDecryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesXtsCipherHw(key1, key2, iv, true);
            }
#endif
            return new AesXtsCipher(key1, key2, iv, true);
        }

        public static ICipher CreateXtsEncryptor(ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
#if HAS_INTRINSICS
            if (IsAesNiSupported() && !preferDotNetCrypto)
            {
                return new AesXtsCipherHw(key1, key2, iv, false);
            }
#endif
            return new AesXtsCipher(key1, key2, iv, false);
        }

        public static void EncryptEcb128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateEcbEncryptor(key, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptEcb128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateEcbDecryptor(key, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptCbc128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateCbcEncryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptCbc128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateCbcDecryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptCtr128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateCtrEncryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptCtr128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key,
            ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateCtrDecryptor(key, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void EncryptXts128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key1,
            ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateXtsEncryptor(key1, key2, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }

        public static void DecryptXts128(ReadOnlySpan<byte> input, Span<byte> output, ReadOnlySpan<byte> key1,
            ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv, bool preferDotNetCrypto = false)
        {
            ICipher cipher = CreateXtsDecryptor(key1, key2, iv, preferDotNetCrypto);

            cipher.Transform(input, output);
        }
    }
}
