using System.IO;
using System.Security.Cryptography;

namespace libhac
{
    public class Crypto
    {
        public static void DecryptEcb(byte[] key, byte[] src, int srcIndex, byte[] dest, int destIndex, int length)
        {
            using (var aes = Aes.Create())
            {
                if(aes == null) throw new CryptographicException("Unable to create AES object");
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

        public static void GenerateKek(byte[] dst, byte[] src, byte[] masterKey, byte[]  kekSeed, byte[] keySeed)
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
    }
}
