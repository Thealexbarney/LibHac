using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using libhac.XTSSharp;

namespace libhac
{
    public class Nca : IDisposable
    {
        public NcaHeader Header { get; private set; }
        public string NcaId { get; set; }
        public string Filename { get; set; }
        public bool HasRightsId { get; private set; }
        public int CryptoType { get; private set; }
        public byte[][] DecryptedKeys { get; } = Util.CreateJaggedArray<byte[][]>(4, 0x10);
        public Stream Stream { get; private set; }
        private bool KeepOpen { get; }

        public List<NcaSection> Sections = new List<NcaSection>();

        public Nca(Keyset keyset, Stream stream, bool keepOpen)
        {
            stream.Position = 0;
            KeepOpen = keepOpen;
            Stream = stream;
            DecryptHeader(keyset, stream);

            CryptoType = Math.Max(Header.CryptoType, Header.CryptoType2);
            if (CryptoType > 0) CryptoType--;

            HasRightsId = !Header.RightsId.IsEmpty();

            if (!HasRightsId)
            {
                DecryptKeyArea(keyset);
            }
            else
            {
                if (keyset.TitleKeys.TryGetValue(Header.RightsId, out var titleKey))
                {
                    Crypto.DecryptEcb(keyset.titlekeks[CryptoType], titleKey, DecryptedKeys[2], 0x10);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var section = ParseSection(i);
                if (section == null) continue;
                Sections.Add(section);
                ValidateSuperblockHash(i);
            }
        }

        public Stream OpenSection(int index, bool raw)
        {
            if (index >= Sections.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var sect = Sections[index];

            long offset = sect.Offset;
            long size = sect.Size;

            if (!raw)
            {
                switch (sect.Header.FsType)
                {
                    case SectionFsType.Pfs0:
                        offset = sect.Offset + sect.Pfs0.Pfs0Offset;
                        size = sect.Pfs0.Pfs0Size;
                        break;
                    case SectionFsType.Romfs:
                        offset = sect.Offset + (long)sect.Header.Romfs.IvfcHeader.LevelHeaders[Romfs.IvfcMaxLevel - 1]
                                     .LogicalOffset;
                        size = (long)sect.Header.Romfs.IvfcHeader.LevelHeaders[Romfs.IvfcMaxLevel - 1].HashDataSize;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }

            Stream.Position = offset;

            switch (sect.Header.CryptType)
            {
                case SectionCryptType.None:
                    break;
                case SectionCryptType.XTS:
                    break;
                case SectionCryptType.CTR:
                    return new RandomAccessSectorStream(new AesCtrStream(Stream, DecryptedKeys[2], offset, size, offset, sect.Header.Ctr), false);
                case SectionCryptType.BKTR:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            return Stream;
        }

        private void DecryptHeader(Keyset keyset, Stream stream)
        {
            byte[] headerBytes = new byte[0xC00];
            var xts = XtsAes128.Create(keyset.header_key);
            using (var headerDec = new RandomAccessSectorStream(new XtsSectorStream(stream, xts, 0x200)))
            {
                headerDec.Read(headerBytes, 0, headerBytes.Length);
            }

            var reader = new BinaryReader(new MemoryStream(headerBytes));

            Header = NcaHeader.Read(reader);
        }

        private void DecryptKeyArea(Keyset keyset)
        {
            for (int i = 0; i < 4; i++)
            {
                Crypto.DecryptEcb(keyset.key_area_keys[CryptoType][Header.KaekInd], Header.EncryptedKeys[i],
                    DecryptedKeys[i], 0x10);
            }
        }

        private NcaSection ParseSection(int index)
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
                sect.Pfs0 = header.Pfs0;
            }

            return sect;
        }

        private void ValidateSuperblockHash(int index)
        {
            if (index >= Sections.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var sect = Sections[index];
            var stream = OpenSection(index, true);

            byte[] expected = null;
            byte[] actual;
            long offset = 0;
            long size = 0;

            switch (sect.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    var pfs0 = sect.Header.Pfs0;
                    expected = pfs0.MasterHash;
                    offset = pfs0.HashTableOffset;
                    size = pfs0.HashTableSize;
                    break;
                case SectionType.Romfs:
                    break;
                case SectionType.Bktr:
                    break;
            }

            if (expected == null) return;

            var hashTable = new byte[size];
            stream.Position = offset;
            stream.Read(hashTable, 0, hashTable.Length);

            using (SHA256 hash = SHA256.Create())
            {
                actual = hash.ComputeHash(hashTable);
            }

            sect.SuperblockHashValidity = Util.ArraysEqual(expected, actual) ? Validity.Valid : Validity.Invalid;
        }

        public void Dispose()
        {
            if (!KeepOpen)
            {
                Stream?.Dispose();
            }
        }
    }

    public class NcaSection
    {
        public Stream Stream { get; set; }
        public NcaFsHeader Header { get; set; }
        public SectionType Type { get; set; }
        public int SectionNum { get; set; }
        public long Offset { get; set; }
        public long Size { get; set; }
        public Validity SuperblockHashValidity { get; set; }

        public Pfs0Superblock Pfs0 { get; set; }
    }
}
