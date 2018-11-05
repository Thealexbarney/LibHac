using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using LibHac.IO;

namespace LibHac
{
    public class Nax0 : IDisposable
    {
        public byte[] Hmac { get; private set; }
        public byte[][] EncKeys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public byte[][] Keys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public byte[] Key { get; } = new byte[0x20];
        public long Length { get; private set; }
        public Storage BaseStorage { get; }
        private bool KeepOpen { get; }

        public Nax0(Keyset keyset, Storage storage, string sdPath, bool keepOpen)
        {
            KeepOpen = keepOpen;
            ReadHeader(storage.AsStream());
            DeriveKeys(keyset, sdPath, storage);

            BaseStorage = new CachedStorage(new Aes128XtsStorage(storage.Slice(0x4000), Key, 0x4000, true), 4, true);
        }

        private void ReadHeader(Stream stream)
        {
            var header = new byte[0x60];
            stream.Read(header, 0, 0x60);
            var reader = new BinaryReader(new MemoryStream(header));

            Hmac = reader.ReadBytes(0x20);
            string magic = reader.ReadAscii(4);
            reader.BaseStream.Position += 4;
            if (magic != "NAX0") throw new InvalidDataException("Not an NAX0 file");
            EncKeys[0] = reader.ReadBytes(0x10);
            EncKeys[1] = reader.ReadBytes(0x10);
            Length = reader.ReadInt64();
        }

        private void DeriveKeys(Keyset keyset, string sdPath, Storage storage)
        {
            var validationHashKey = new byte[0x60];
            storage.Read(validationHashKey, 0x20);

            // Try both the NCA and save key sources and pick the one that works
            for (int k = 0; k < 2; k++)
            {
                var naxSpecificKeys = Util.CreateJaggedArray<byte[][]>(2, 0x10);
                var hashKey = new byte[0x10];
                Array.Copy(keyset.SdCardKeys[k], hashKey, 0x10);

                // Use the sd path to generate the kek for this NAX0
                var hash = new HMACSHA256(hashKey);
                byte[] sdPathBytes = Encoding.ASCII.GetBytes(sdPath);
                byte[] checksum = hash.ComputeHash(sdPathBytes, 0, sdPathBytes.Length);
                Array.Copy(checksum, 0, naxSpecificKeys[0], 0, 0x10);
                Array.Copy(checksum, 0x10, naxSpecificKeys[1], 0, 0x10);

                // Decrypt this NAX0's keys
                Crypto.DecryptEcb(naxSpecificKeys[0], EncKeys[0], Keys[0], 0x10);
                Crypto.DecryptEcb(naxSpecificKeys[1], EncKeys[1], Keys[1], 0x10);
                Array.Copy(Keys[0], 0, Key, 0, 0x10);
                Array.Copy(Keys[1], 0, Key, 0x10, 0x10);

                // Copy the decrypted keys into the NAX0 header and use that for the HMAC key
                // for validating that the keys are correct
                Array.Copy(Keys[0], 0, validationHashKey, 8, 0x10);
                Array.Copy(Keys[1], 0, validationHashKey, 0x18, 0x10);

                var validationHash = new HMACSHA256(validationHashKey);
                byte[] validationMac = validationHash.ComputeHash(keyset.SdCardKeys[k], 0x10, 0x10);

                if (Util.ArraysEqual(Hmac, validationMac))
                {
                    return;
                }
            }

            throw new ArgumentException("NAX0 key derivation failed.");
        }

        public void Dispose()
        {
            if (!KeepOpen)
            {
                BaseStorage?.Dispose();
            }
        }
    }
}
