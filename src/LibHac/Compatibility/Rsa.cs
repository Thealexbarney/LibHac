#if NETFRAMEWORK

using System;
using System.Numerics;
using LibHac.Crypto;

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

            var message = new byte[rsaLen];

            BigInteger decInt = BigInteger.ModPow(CryptoOld.GetBigInteger(signature), new BigInteger(65537), CryptoOld.GetBigInteger(modulus));
            byte[] decBytes = decInt.ToByteArray();

            if (decBytes[0] != 0xBC) return false;

            Array.Reverse(decBytes);
            Array.Copy(decBytes, 0, message, message.Length - decBytes.Length, decBytes.Length);

            var hashBuf = new byte[0x24];
            Array.Copy(message, hashOffset, hashBuf, 0, digestLen);

            ref byte seed = ref hashBuf[0x23];

            Span<byte> digestBuffer = stackalloc byte[Sha256.DigestSize];
            
            for (int i = 0; i < hashOffset; i += 0x20)
            {
                Sha256.GenerateSha256Hash(hashBuf, digestBuffer);
                Util.XorArrays(message.AsSpan(i, digestLen), digestBuffer);
                seed++;
            }

            message[0] &= 0x7F;

            if (!Util.IsEmpty(message.AsSpan(0, padEnd)) || message[padEnd] != 1)
            {
                return false;
            }

            Span<byte> prefix = stackalloc byte[8];
            Span<byte> digest = stackalloc byte[Sha256.DigestSize];

            Sha256.GenerateSha256Hash(data, digest);

            IHash sha2 = Sha256.CreateSha256Generator();
            sha2.Initialize();

            sha2.Update(prefix);
            sha2.Update(digest);
            sha2.Update(message.AsSpan(saltOffset, digestLen));

            sha2.GetHash(digest);

            return Util.SpansEqual(hashBuf.AsSpan(0, 0x20), digest);
        }
    }
}
#endif
