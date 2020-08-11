using System;
using System.Security.Cryptography;

namespace LibHac.Crypto
{
    public static class Rsa
    {
        public static bool VerifyRsa2048PssSha256(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> modulus,
            ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> message)
        {
            try
            {
                var param = new RSAParameters { Modulus = modulus.ToArray(), Exponent = exponent.ToArray() };

                using (var rsa = RSA.Create(param))
                {
                    return rsa.VerifyData(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }

        public static bool VerifyRsa2048PssSha256WithHash(ReadOnlySpan<byte> signature, ReadOnlySpan<byte> modulus,
            ReadOnlySpan<byte> exponent, ReadOnlySpan<byte> message)
        {
            try
            {
                var param = new RSAParameters { Modulus = modulus.ToArray(), Exponent = exponent.ToArray() };

                using (var rsa = RSA.Create(param))
                {
                    return rsa.VerifyHash(message, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pss);
                }
            }
            catch (CryptographicException)
            {
                return false;
            }
        }
    }
}
