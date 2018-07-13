using System;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using libhac.XTSSharp;

namespace libhac
{
    public class Crypto
    {
        public static void DecryptEcb(byte[] key, byte[] src, int srcIndex, byte[] dest, int destIndex, int length)
        {
            using (var aes = Aes.Create())
            {
                if (aes == null) throw new CryptographicException("Unable to create AES object");
                aes.Key = key;
                aes.Mode = CipherMode.ECB;
                aes.Padding = PaddingMode.None;
                var dec = aes.CreateDecryptor();
                using (var ms = new MemoryStream(dest, destIndex, length))
                using (var cs = new CryptoStream(ms, dec, CryptoStreamMode.Write))
                {
                    cs.Write(src, srcIndex, length);
                    cs.FlushFinalBlock();
                }
            }
        }

        public static void DecryptEcb(byte[] key, byte[] src, byte[] dest, int length) =>
            DecryptEcb(key, src, 0, dest, 0, length);

        public static void GenerateKek(byte[] dst, byte[] src, byte[] masterKey, byte[] kekSeed, byte[] keySeed)
        {
            var kek = new byte[0x10];
            var srcKek = new byte[0x10];
            DecryptEcb(masterKey, kekSeed, kek, 0x10);
            DecryptEcb(kek, src, srcKek, 0x10);

            if (keySeed != null)
            {
                DecryptEcb(srcKek, keySeed, dst, 0x10);
            }
        }

        internal static BigInteger GetBigInteger(byte[] bytes)
        {
            byte[] signPadded = new byte[bytes.Length + 1];
            Buffer.BlockCopy(bytes, 0, signPadded, 1, bytes.Length);
            Array.Reverse(signPadded);
            return new BigInteger(signPadded);
        }

        public static RsaKey DecryptRsaKey(byte[] encryptedKey, byte[] kek)
        {
            var counter = new byte[0x10];
            Array.Copy(encryptedKey, counter, 0x10);
            var body = new byte[0x230];
            Array.Copy(encryptedKey, 0x10, body, 0, 0x230);
            var dec = new byte[0x230];

            using (var streamDec = new RandomAccessSectorStream(new AesCtrStream(new MemoryStream(body), kek, counter)))
            {
                streamDec.Read(dec, 0, dec.Length);
            }

            var d = new byte[0x100];
            var n = new byte[0x100];
            var e = new byte[4];
            Array.Copy(dec, 0, d, 0, 0x100);
            Array.Copy(dec, 0x100, n, 0, 0x100);
            Array.Copy(dec, 0x200, e, 0, 4);

            var dInt = GetBigInteger(d);
            var nInt = GetBigInteger(n);
            var eInt = GetBigInteger(e);

            var test = new BigInteger(12345678);
            var testEnc = BigInteger.ModPow(test, dInt, nInt);
            var testDec = BigInteger.ModPow(testEnc, eInt, nInt);

            if (test != testDec)
            {
                throw new InvalidDataException("Could not verify RSA key pair");
            }

            return new RsaKey(n, e, d);
        }

        public static byte[] DecryptTitleKey(byte[] titleKeyblock, RsaKey rsaKey)
        {
            if (rsaKey == null) return new byte[0x10];

            var tikInt = GetBigInteger(titleKeyblock);
            var decInt = BigInteger.ModPow(tikInt, rsaKey.DInt, rsaKey.NInt);
            var decBytes = decInt.ToByteArray();
            Array.Reverse(decBytes);
            var decBlock = new byte[0x100];
            Array.Copy(decBytes, 0, decBlock, decBlock.Length - decBytes.Length, decBytes.Length);
            return UnwrapTitleKey(decBlock);
        }

        public static byte[] UnwrapTitleKey(byte[] data)
        {
            var expectedLabelHash = Ticket.LabelHash;
            var salt = new byte[0x20];
            var db = new byte[0xdf];
            Array.Copy(data, 1, salt, 0, salt.Length);
            Array.Copy(data, 0x21, db, 0, db.Length);

            CalculateMgf1AndXor(salt, db);
            CalculateMgf1AndXor(db, salt);

            for (int i = 0; i < 0x20; i++)
            {
                if (expectedLabelHash[i] != db[i])
                {
                    return null;
                }
            }

            int keyOffset = 0x20;
            while (keyOffset < 0xdf)
            {
                var value = db[keyOffset++];
                if (value == 1)
                {
                    break;
                }
                if (value != 0)
                {
                    return null;
                }
            }

            if (keyOffset + 0x10 > db.Length) return null;

            var key = new byte[0x10];
            Array.Copy(db, keyOffset, key, 0, 0x10);

            return key;
        }

        private static void CalculateMgf1AndXor(byte[] masked, byte[] seed)
        {
            SHA256 hash = SHA256.Create();
            var hashBuf = new byte[seed.Length + 4];
            Array.Copy(seed, hashBuf, seed.Length);

            int maskedSize = masked.Length;
            int roundNum = 0;
            int pOut = 0;

            while (maskedSize != 0)
            {
                hashBuf[hashBuf.Length - 4] = (byte)(roundNum >> 24);
                hashBuf[hashBuf.Length - 3] = (byte)(roundNum >> 16);
                hashBuf[hashBuf.Length - 2] = (byte)(roundNum >> 8);
                hashBuf[hashBuf.Length - 1] = (byte)roundNum;
                roundNum++;

                byte[] mask = hash.ComputeHash(hashBuf);
                int curSize = Math.Min(maskedSize, 0x20);

                for (int i = 0; i < curSize; i++, pOut++)
                {
                    masked[pOut] ^= mask[i];
                }

                maskedSize -= curSize;
            }
        }
    }

    public class RsaKey
    {
        public byte[] N { get; }
        public byte[] E { get; }
        public byte[] D { get; }
        public BigInteger NInt { get; }
        public BigInteger EInt { get; }
        public BigInteger DInt { get; }

        public RsaKey(byte[] n, byte[] e, byte[] d)
        {
            N = n;
            E = e;
            D = d;
            NInt = Crypto.GetBigInteger(n);
            EInt = Crypto.GetBigInteger(e);
            DInt = Crypto.GetBigInteger(d);
        }
    }
}
