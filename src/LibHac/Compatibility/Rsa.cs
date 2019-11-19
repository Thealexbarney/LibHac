#if NETFRAMEWORK

using System;
using System.Numerics;
using System.Security.Cryptography;

namespace LibHac.Compatibility
{
    internal static class Rsa
    {
        public static bool Rsa2048PssVerifyMono(byte[] data, byte[] signature, byte[] modulus)
        {
            const int rsaLen = 0x100;
            const int digestLen = 0x20;

            const int hashOffset = rsaLen - digestLen - 1;
            const int saltOffset = hashOffset - digestLen;
            const int padEnd = saltOffset - 1;

            SHA256 sha = SHA256.Create();
            var message = new byte[rsaLen];

            BigInteger decInt = BigInteger.ModPow(CryptoOld.GetBigInteger(signature), new BigInteger(65537), CryptoOld.GetBigInteger(modulus));
            byte[] decBytes = decInt.ToByteArray();

            if (decBytes[0] != 0xBC) return false;

            Array.Reverse(decBytes);
            Array.Copy(decBytes, 0, message, message.Length - decBytes.Length, decBytes.Length);

            var hashBuf = new byte[0x24];
            Array.Copy(message, hashOffset, hashBuf, 0, digestLen);

            ref byte seed = ref hashBuf[0x23];

            for (int i = 0; i < hashOffset; i += 0x20)
            {
                Util.XorArrays(message.AsSpan(i, digestLen), sha.ComputeHash(hashBuf));
                seed++;
            }

            message[0] &= 0x7F;

            if (!Util.IsEmpty(message.AsSpan(0, padEnd)) || message[padEnd] != 1)
            {
                return false;
            }

            var prefix = new byte[8];
            byte[] digest = sha.ComputeHash(data);

            sha.TransformBlock(prefix, 0, prefix.Length, null, 0);
            sha.TransformBlock(digest, 0, digestLen, null, 0);
            sha.TransformFinalBlock(message, saltOffset, digestLen);

            return Util.SpansEqual(hashBuf.AsSpan(0, 0x20), sha.Hash);
        }
    }
}
#endif
