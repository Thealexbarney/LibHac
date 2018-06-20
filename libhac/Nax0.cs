using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using libhac.XTSSharp;

namespace libhac
{
    public class Nax0
    {
        public List<string> Files = new List<string>();
        public byte[] Hmac { get; set; }
        public byte[][] EncKeys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public byte[][] Keys { get; } = Util.CreateJaggedArray<byte[][]>(2, 0x10);
        public long Length { get; set; }
        public Stream Stream { get; }
        private List<Stream> Streams = new List<Stream>();

        public Nax0(Keyset keyset, string path, string sdPath)
        {
            if (Directory.Exists(path))
            {
                while (true)
                {
                    var partName = Path.Combine(path, $"{Files.Count:D2}");
                    if (!File.Exists(partName)) break;

                    Files.Add(partName);
                }

            }
            else if (File.Exists(path))
            {
                Files.Add(path);
            }
            else
            {
                throw new FileNotFoundException("Could not find the input file or directory");
            }

            foreach (var file in Files)
            {
                Streams.Add(new FileStream(file, FileMode.Open));
            }

            var stream = new CombinationStream(Streams);
            ReadHeader(stream);

            for (int k = 0; k < 2; k++)
            {
                var naxSpecificKeys = Util.CreateJaggedArray<byte[][]>(2, 0x10);
                var hashKey2 = new byte[0x10];
                Array.Copy(keyset.sd_card_keys[k], hashKey2, 0x10);

                var hash2 = new HMACSHA256(hashKey2);
                var sdPathBytes = Encoding.ASCII.GetBytes(sdPath);
                var checksum = hash2.ComputeHash(sdPathBytes, 0, sdPathBytes.Length);
                Array.Copy(checksum, 0, naxSpecificKeys[0], 0, 0x10);
                Array.Copy(checksum, 0x10, naxSpecificKeys[1], 0, 0x10);

                Crypto.DecryptEcb(naxSpecificKeys[0], EncKeys[0], Keys[0], 0x10);
                Crypto.DecryptEcb(naxSpecificKeys[1], EncKeys[1], Keys[1], 0x10);
            }

            stream.Position = 0x20;
            var hashKey = new byte[0x60];
            stream.Read(hashKey, 0, 0x60);
            Array.Copy(Keys[0], 0, hashKey, 8, 0x10);
            Array.Copy(Keys[1], 0, hashKey, 0x18, 0x10);

            var hash = new HMACSHA256(hashKey);
            var validationMac = hash.ComputeHash(keyset.sd_card_keys[1], 0x10, 0x10);
            var isValid = Util.ArraysEqual(Hmac, validationMac);

            if (!isValid) throw new ArgumentException("NAX0 key derivation failed.");

            stream.Position = 0x4000;

            var xts = XtsAes128.Create(Keys[0], Keys[1]);
            Stream = new RandomAccessSectorStream(new XtsSectorStream(stream, xts, 0x4000, 0x4000));
        }

        private void ReadHeader(Stream nax0)
        {
            var reader = new BinaryReader(nax0);
            nax0.Position = 0;

            Hmac = reader.ReadBytes(0x20);
            nax0.Position += 8; //todo check magic
            EncKeys[0] = reader.ReadBytes(0x10);
            EncKeys[1] = reader.ReadBytes(0x10);
            Length = reader.ReadInt64();
        }
    }
}
