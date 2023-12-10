using System;
using System.Security.Cryptography;
using LibHac.Common;
using LibHac.Crypto;

namespace LibHac.Tools.Crypto;

public static class CryptoOld
{
    public static Validity Rsa2048Pkcs1Verify(byte[] data, byte[] signature, byte[] modulus) =>
        Rsa.VerifyRsa2048Pkcs1Sha256(signature, modulus, [1, 0, 1], data)
            ? Validity.Valid
            : Validity.Invalid;

    public static Validity Rsa2048PssVerify(byte[] data, byte[] signature, byte[] modulus) =>
        Rsa.VerifyRsa2048PssSha256(signature, modulus, [1, 0, 1], data)
            ? Validity.Valid
            : Validity.Invalid;

    public static byte[] DecryptRsaOaep(byte[] data, RSAParameters rsaParams)
    {
        var rsa = RSA.Create();

        rsa.ImportParameters(rsaParams);
        return rsa.Decrypt(data, RSAEncryptionPadding.OaepSHA256);
    }

    public static bool DecryptRsaOaep(ReadOnlySpan<byte> data, Span<byte> destination, RSAParameters rsaParams, out int bytesWritten)
    {
        using (var rsa = RSA.Create())
        {
            try
            {
                rsa.ImportParameters(rsaParams);

                return rsa.TryDecrypt(data, destination, RSAEncryptionPadding.OaepSHA256, out bytesWritten);
            }
            catch (CryptographicException)
            {
                bytesWritten = 0;
                return false;
            }
        }
    }
}