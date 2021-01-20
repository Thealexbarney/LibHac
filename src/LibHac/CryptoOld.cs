using System;
using System.IO;
using System.Security.Cryptography;
using LibHac.Crypto;
using LibHac.FsSystem;

using Aes = LibHac.Crypto.Aes;

namespace LibHac
{
    public static class CryptoOld
    {
        public static void GenerateKek(byte[] key, byte[] src, byte[] dest, byte[] kekSeed, byte[] keySeed)
        {
            byte[] kek = new byte[Aes.KeySize128];
            byte[] srcKek = new byte[Aes.KeySize128];

            Aes.DecryptEcb128(kekSeed, kek, key);
            Aes.DecryptEcb128(src, srcKek, kek);

            if (keySeed != null)
            {
                Aes.DecryptEcb128(keySeed, dest, srcKek);
            }
            else
            {
                Array.Copy(srcKek, dest, Aes.KeySize128);
            }
        }

        public static RSAParameters DecryptRsaKey(byte[] encryptedKey, byte[] kek)
        {
            byte[] counter = new byte[0x10];
            Array.Copy(encryptedKey, counter, 0x10);
            byte[] key = new byte[0x230];
            Array.Copy(encryptedKey, 0x10, key, 0, 0x230);

            new Aes128CtrTransform(kek, counter).TransformBlock(key);

            byte[] d = new byte[0x100];
            byte[] n = new byte[0x100];
            byte[] e = new byte[4];
            Array.Copy(key, 0, d, 0, 0x100);
            Array.Copy(key, 0x100, n, 0, 0x100);
            Array.Copy(key, 0x200, e, 0, 4);

            RSAParameters rsaParams = Rsa.RecoverParameters(n, e, d);
            TestRsaKey(rsaParams);
            return rsaParams;
        }

        private static void TestRsaKey(RSAParameters keyParams)
        {
            var rsa = new RSACryptoServiceProvider();
            rsa.ImportParameters(keyParams);

            byte[] test = { 12, 34, 56, 78 };
            byte[] testEnc = rsa.Encrypt(test, false);
            byte[] testDec = rsa.Decrypt(testEnc, false);

            if (!Utilities.ArraysEqual(test, testDec))
            {
                throw new InvalidDataException("Could not verify RSA key pair");
            }
        }

        public static Validity Rsa2048Pkcs1Verify(byte[] data, byte[] signature, byte[] modulus) =>
            Rsa.VerifyRsa2048Pkcs1Sha256(signature, modulus, new byte[] { 1, 0, 1 }, data)
                ? Validity.Valid
                : Validity.Invalid;

        public static Validity Rsa2048PssVerify(byte[] data, byte[] signature, byte[] modulus) =>
            Rsa.VerifyRsa2048PssSha256(signature, modulus, new byte[] { 1, 0, 1 }, data)
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
}