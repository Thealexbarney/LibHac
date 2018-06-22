using System;
using System.Collections.Generic;
using System.IO;
using libhac.XTSSharp;

namespace libhac
{
    public class Nca
    {
        public NcaHeader Header { get; private set; }
        public string Name { get; set; }
        public bool HasRightsId { get; private set; }
        public int CryptoType { get; private set; }
        public byte[][] DecryptedKeys { get; } = Util.CreateJaggedArray<byte[][]>(4, 0x10);
        public Stream Stream { get; private set; }

        public List<NcaSection> Sections = new List<NcaSection>();

        public Nca(Keyset keyset, Stream stream)
        {
            Stream = stream;
            ReadHeader(keyset, stream);

            CryptoType = Math.Max(Header.CryptoType, Header.CryptoType2);
            if (CryptoType > 0) CryptoType--;

            HasRightsId = !Header.RightsId.IsEmpty();

            if (!HasRightsId)
            {
                DecryptKeyArea(keyset);
            }

            for (int i = 0; i < 4; i++)
            {
                var section = ParseSection(keyset, stream, i);
                if (section != null) Sections.Add(section);
            }
        }

        public Stream OpenSection(int index)
        {
            if (index >= Sections.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var sect = Sections[index];
            Stream.Position = sect.Offset;

            switch (sect.Header.CryptType)
            {
                case SectionCryptType.None:
                    break;
                case SectionCryptType.XTS:
                    break;
                case SectionCryptType.CTR:
                    return new RandomAccessSectorStream(new AesCtrStream(Stream, DecryptedKeys[2], sect.Offset, sect.Size, sect.Offset));
                case SectionCryptType.BKTR:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return null;
        }

        private void ReadHeader(Keyset keyset, Stream stream)
        {
            stream.Position = 0;
            var xts = XtsAes128.Create(keyset.header_key);
            var headerDec = new RandomAccessSectorStream(new XtsSectorStream(stream, xts, 0x200));
            var reader = new BinaryReader(headerDec);

            Header = NcaHeader.Read(reader);

            headerDec.Close();
        }

        private void DecryptKeyArea(Keyset keyset)
        {
            for (int i = 0; i < 4; i++)
            {
                Crypto.DecryptEcb(keyset.key_area_keys[CryptoType][Header.KaekInd], Header.EncryptedKeys[i],
                    DecryptedKeys[i], 0x10);
            }
        }

        private NcaSection ParseSection(Keyset keyset, Stream stream, int index)
        {
            var entry = Header.SectionEntries[index];
            var header = Header.FsHeaders[index];
            if (entry.MediaStartOffset == 0) return null;

            var sect = new NcaSection();

            sect.SectionNum = index;
            sect.Offset = Util.MediaToReal(entry.MediaStartOffset);
            sect.Size = Util.MediaToReal(entry.MediaEndOffset) - sect.Offset;
            sect.Header = header;
            sect.Type = header.Type;

            if (sect.Type == SectionType.Pfs0)
            {
                sect.Pfs0 = new Pfs0 { Superblock = header.Pfs0 };
            }

            return sect;
        }
    }

    public class NcaSection
    {
        public Stream Stream;
        public NcaFsHeader Header { get; set; }
        public SectionType Type { get; set; }
        public int SectionNum { get; set; }
        public long Offset { get; set; }
        public long Size { get; set; }

        public Pfs0 Pfs0 { get; set; }
    }
}
