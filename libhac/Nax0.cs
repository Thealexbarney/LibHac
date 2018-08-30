using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using libhac.Streams;
using libhac.XTSSharp;

namespace libhac
{
    public class Nax0 : IDisposable
    {
        public byte[] Hmac { get; private set; }
        public byte[][] EncKeys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public byte[][] Keys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public long Length { get; private set; }
        public Stream Stream { get; }
        private bool KeepOpen { get; }

        public Nax0(Keyset keyset, Stream stream, string sdPath, bool keepOpen)
        {
            stream.Position = 0;
            KeepOpen = keepOpen;
            ReadHeader(stream);
            DeriveKeys(keyset, sdPath);
            ValidateKeys(keyset, stream);

            stream.Position = 0x4000;
            var xts = XtsAes128.Create(Keys[0], Keys[1]);
            Stream = new RandomAccessSectorStream(new XtsSectorStream(stream, xts, 0x4000, 0x4000), keepOpen);
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

        private void DeriveKeys(Keyset keyset, string sdPath)
        {
            for (int k = 0; k < 2; k++)
            {
                var naxSpecificKeys = Util.CreateJaggedArray<byte[][]>(2, 0x10);
                var hashKey = new byte[0x10];
                Array.Copy(keyset.sd_card_keys[k], hashKey, 0x10);

                var hash = new HMACSHA256(hashKey);
                var sdPathBytes = Encoding.ASCII.GetBytes(sdPath);
                var checksum = hash.ComputeHash(sdPathBytes, 0, sdPathBytes.Length);
                Array.Copy(checksum, 0, naxSpecificKeys[0], 0, 0x10);
                Array.Copy(checksum, 0x10, naxSpecificKeys[1], 0, 0x10);

                Crypto.DecryptEcb(naxSpecificKeys[0], EncKeys[0], Keys[0], 0x10);
                Crypto.DecryptEcb(naxSpecificKeys[1], EncKeys[1], Keys[1], 0x10);
            }
        }

        private void ValidateKeys(Keyset keyset, Stream stream)
        {
            stream.Position = 0x20;
            var hashKey = new byte[0x60];
            stream.Read(hashKey, 0, 0x60);
            Array.Copy(Keys[0], 0, hashKey, 8, 0x10);
            Array.Copy(Keys[1], 0, hashKey, 0x18, 0x10);

            var hash = new HMACSHA256(hashKey);
            var validationMac = hash.ComputeHash(keyset.sd_card_keys[1], 0x10, 0x10);
            var isValid = Util.ArraysEqual(Hmac, validationMac);

            if (!isValid) throw new ArgumentException("NAX0 key derivation failed.");
        }

        public void Dispose()
        {
            if (!KeepOpen)
            {
                Stream?.Dispose();
            }
        }
    }
}
