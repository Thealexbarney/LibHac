using System.IO;
using libhac.XTSSharp;

namespace libhac
{
    public class Nca
    {
        public byte[] Signature1 { get; set; } // RSA-PSS signature over header with fixed key.
        public byte[] Signature2 { get; set; } // RSA-PSS signature over header with key in NPDM.
        public string Magic { get; set; }
        public byte Distribution { get; set; } // System vs gamecard.
        public byte ContentType { get; set; }
        public byte CryptoType { get; set; } // Which keyblob (field 1)
        public byte KaekInd { get; set; } // Which kaek index?
        public ulong NcaSize { get; set; } // Entire archive size.
        public ulong TitleId { get; set; }
        public uint SdkVersion { get; set; } // What SDK was this built with?
        public byte CryptoType2 { get; set; } // Which keyblob (field 2)
        public byte[] RightsId { get; set; }
        public string Name { get; set; }

        public Nca(Keyset keyset, Stream stream)
        {
            ReadHeader(keyset, stream);
        }

        private void ReadHeader(Keyset keyset, Stream stream)
        {
            stream.Position = 0;
            var xts = XtsAes128.Create(keyset.header_key);
            var header = new RandomAccessSectorStream(new XtsSectorStream(stream, xts, 0x200));
            var reader = new BinaryReader(header);

            Signature1 = reader.ReadBytes(0x100);
            Signature2 = reader.ReadBytes(0x100);
            Magic = reader.ReadAscii(4);
            if (Magic != "NCA3") throw new InvalidDataException("Not an NCA3 file");
            Distribution = reader.ReadByte();
            ContentType = reader.ReadByte();
            CryptoType = reader.ReadByte();
            KaekInd = reader.ReadByte();
            NcaSize = reader.ReadUInt64();
            TitleId = reader.ReadUInt64();
            header.Position += 4;

            SdkVersion = reader.ReadUInt32();
            CryptoType2 = reader.ReadByte();
            header.Position += 0xF;

            RightsId = reader.ReadBytes(0x10);
            header.Close();
        }
    }
}
