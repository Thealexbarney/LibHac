using System;

#if HAS_INTRINSICS
using System.Runtime.Intrinsics.X86;
#endif

namespace LibHac.Crypto2
{
    public static class AesCrypto
    {
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
    }
}
