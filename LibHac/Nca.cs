using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using LibHac.Streams;
using LibHac.XTSSharp;

namespace LibHac
{
    public class Nca : IDisposable
    {
        public NcaHeader Header { get; private set; }
        public string NcaId { get; set; }
        public string Filename { get; set; }
        public bool HasRightsId { get; private set; }
        public int CryptoType { get; private set; }
        public byte[][] DecryptedKeys { get; } = Util.CreateJaggedArray<byte[][]>(4, 0x10);
        public byte[] TitleKey { get; }
        public byte[] TitleKeyDec { get; } = new byte[0x10];
        private Stream Stream { get; }
        private SharedStreamSource StreamSource { get; }
        private bool KeepOpen { get; }
        private Nca BaseNca { get; set; }

        public NcaSection[] Sections { get; } = new NcaSection[4];

        public Nca(Keyset keyset, Stream stream, bool keepOpen)
        {
            stream.Position = 0;
            KeepOpen = keepOpen;
            Stream = stream;
            StreamSource = new SharedStreamSource(stream);
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
                    TitleKey = titleKey;
                    Crypto.DecryptEcb(keyset.Titlekeks[CryptoType], titleKey, TitleKeyDec, 0x10);
                    DecryptedKeys[2] = TitleKeyDec;
                }
            }

            for (int i = 0; i < 4; i++)
            {
                var section = ParseSection(i);
                if (section == null) continue;
                Sections[i] = section;
                ValidateSuperblockHash(i);
            }

            foreach (var pfsSection in Sections.Where(x => x != null && x.Type == SectionType.Pfs0))
            {
                var sectionStream = OpenSection(pfsSection.SectionNum, false);
                if (sectionStream == null) continue;

                var pfs = new Pfs(sectionStream);
                if (!pfs.FileExists("main.npdm")) continue;

                pfsSection.IsExefs = true;
            }
        }

        public Stream GetStream()
        {
            return StreamSource.CreateStream();
        }

        public Stream OpenSection(int index, bool raw)
        {
            if (Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
            var sect = Sections[index];

            if (sect.SuperblockHashValidity == Validity.Invalid) return null;

            long offset = sect.Offset;
            long size = sect.Size;

            Stream rawStream = StreamSource.CreateStream(offset, size);

            switch (sect.Header.CryptType)
            {
                case SectionCryptType.None:
                    break;
                case SectionCryptType.XTS:
                    break;
                case SectionCryptType.CTR:
                    rawStream = new RandomAccessSectorStream(new Aes128CtrStream(rawStream, DecryptedKeys[2], offset, sect.Header.Ctr), false);
                    break;
                case SectionCryptType.BKTR:
                    rawStream = new RandomAccessSectorStream(
                        new BktrCryptoStream(rawStream, DecryptedKeys[2], 0, size, offset, sect.Header.Ctr, sect.Header.Bktr),
                        false);
                    if (BaseNca == null)
                    {
                        return rawStream;
                    }
                    else
                    {
                        var baseSect = BaseNca.Sections.FirstOrDefault(x => x.Type == SectionType.Romfs);
                        if (baseSect == null) throw new InvalidDataException("Base NCA has no RomFS section");

                        var baseStream = BaseNca.OpenSection(baseSect.SectionNum, true);
                        rawStream = new Bktr(rawStream, baseStream, sect);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (raw) return rawStream;

            switch (sect.Header.Type)
            {
                case SectionType.Pfs0:
                    offset = sect.Pfs0.Superblock.Pfs0Offset;
                    size = sect.Pfs0.Superblock.Pfs0Size;
                    break;
                case SectionType.Romfs:
                    offset = sect.Header.Romfs.IvfcHeader.LevelHeaders[Romfs.IvfcMaxLevel - 1].LogicalOffset;
                    size = sect.Header.Romfs.IvfcHeader.LevelHeaders[Romfs.IvfcMaxLevel - 1].HashDataSize;
                    break;
                case SectionType.Bktr:
                    offset = sect.Header.Bktr.IvfcHeader.LevelHeaders[Romfs.IvfcMaxLevel - 1].LogicalOffset;
                    size = sect.Header.Bktr.IvfcHeader.LevelHeaders[Romfs.IvfcMaxLevel - 1].HashDataSize;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            return new SubStream(rawStream, offset, size);

        }

        public void SetBaseNca(Nca baseNca) => BaseNca = baseNca;

        private void DecryptHeader(Keyset keyset, Stream stream)
        {
            byte[] headerBytes = new byte[0xC00];
            var xts = XtsAes128.Create(keyset.HeaderKey);
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
                Crypto.DecryptEcb(keyset.KeyAreaKeys[CryptoType][Header.KaekInd], Header.EncryptedKeys[i],
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
                sect.Pfs0 = new Pfs0Section();
                sect.Pfs0.Superblock = header.Pfs;
            }
            else if (sect.Type == SectionType.Romfs)
            {
                ProcessIvfcSection(sect);
            }

            return sect;
        }

        private void ProcessIvfcSection(NcaSection sect)
        {
            sect.Romfs = new RomfsSection();
            sect.Romfs.Superblock = sect.Header.Romfs;
            var headers = sect.Romfs.Superblock.IvfcHeader.LevelHeaders;

            for (int i = 0; i < Romfs.IvfcMaxLevel; i++)
            {
                var level = new IvfcLevel();
                sect.Romfs.IvfcLevels[i] = level;
                var header = headers[i];
                level.DataOffset = header.LogicalOffset;
                level.DataSize = header.HashDataSize;
                level.HashBlockSize = 1 << header.BlockSize;
                level.HashBlockCount = Util.DivideByRoundUp(level.DataSize, level.HashBlockSize);
                level.HashSize = level.HashBlockCount * 0x20;

                if (i != 0)
                {
                    level.HashOffset = sect.Romfs.IvfcLevels[i - 1].DataOffset;
                }
            }
        }

        private void CheckBktrKey(NcaSection sect)
        {
            var offset = sect.Header.Bktr.SubsectionHeader.Offset;
            using (var streamDec = new RandomAccessSectorStream(new Aes128CtrStream(GetStream(), DecryptedKeys[2], sect.Offset, sect.Size, sect.Offset, sect.Header.Ctr)))
            {
                var reader = new BinaryReader(streamDec);
                streamDec.Position = offset + 8;
                var size = reader.ReadInt64();

                if (size != offset)
                {
                    sect.SuperblockHashValidity = Validity.Invalid;
                }
            }
        }

        private void ValidateSuperblockHash(int index)
        {
            if (Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
            var sect = Sections[index];

            byte[] expected = null;
            byte[] actual;
            long offset = 0;
            long size = 0;

            switch (sect.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    var pfs0 = sect.Header.Pfs;
                    expected = pfs0.MasterHash;
                    offset = pfs0.HashTableOffset;
                    size = pfs0.HashTableSize;
                    break;
                case SectionType.Romfs:
                    var ivfc = sect.Header.Romfs.IvfcHeader;
                    expected = ivfc.MasterHash;
                    offset = ivfc.LevelHeaders[0].LogicalOffset;
                    size = 1 << ivfc.LevelHeaders[0].BlockSize;
                    break;
                case SectionType.Bktr:
                    CheckBktrKey(sect);
                    return;
            }

            var stream = OpenSection(index, true);
            if (stream == null) return;
            if (expected == null) return;

            var hashTable = new byte[size];
            stream.Position = offset;
            stream.Read(hashTable, 0, hashTable.Length);

            using (SHA256 hash = SHA256.Create())
            {
                actual = hash.ComputeHash(hashTable);
            }

            var validity = Util.ArraysEqual(expected, actual) ? Validity.Valid : Validity.Invalid;

            sect.SuperblockHashValidity = validity;
            if (sect.Type == SectionType.Romfs) sect.Romfs.IvfcLevels[0].HashValidity = validity;
        }

        public void VerifySection(int index, IProgressReport logger = null)
        {
            if (Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
            var sect = Sections[index];
            var stream = OpenSection(index, true);
            logger?.LogMessage($"Verifying section {index}...");

            switch (sect.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    VerifyPfs0(stream, sect.Pfs0, logger);
                    break;
                case SectionType.Romfs:
                    VerifyIvfc(stream, sect.Romfs.IvfcLevels, logger);
                    break;
                case SectionType.Bktr:
                    break;
            }
        }

        private void VerifyPfs0(Stream section, Pfs0Section pfs0, IProgressReport logger = null)
        {
            var sb = pfs0.Superblock;
            var table = new byte[sb.HashTableSize];
            section.Position = sb.HashTableOffset;
            section.Read(table, 0, table.Length);

            pfs0.Validity = VerifyHashTable(section, table, sb.Pfs0Offset, sb.Pfs0Size, sb.BlockSize, false, logger);
        }

        private void VerifyIvfc(Stream section, IvfcLevel[] levels, IProgressReport logger = null)
        {
            for (int i = 1; i < levels.Length; i++)
            {
                logger?.LogMessage($"    Verifying IVFC Level {i}...");
                var level = levels[i];
                var table = new byte[level.HashSize];
                section.Position = level.HashOffset;
                section.Read(table, 0, table.Length);
                level.HashValidity =
                    VerifyHashTable(section, table, level.DataOffset, level.DataSize, level.HashBlockSize, true, logger);
            }
        }

        private Validity VerifyHashTable(Stream section, byte[] hashTable, long dataOffset, long dataLen, long blockSize, bool isFinalBlockFull, IProgressReport logger = null)
        {
            const int hashSize = 0x20;
            var currentBlock = new byte[blockSize];
            var expectedHash = new byte[hashSize];
            var blockCount = Util.DivideByRoundUp(dataLen, blockSize);
            int curBlockSize = (int)blockSize;
            section.Position = dataOffset;
            logger?.SetTotal(blockCount);

            using (SHA256 sha256 = SHA256.Create())
            {
                for (long i = 0; i < blockCount; i++)
                {
                    var remaining = (dataLen - i * blockSize);
                    if (remaining < blockSize)
                    {
                        Array.Clear(currentBlock, 0, currentBlock.Length);
                        if (!isFinalBlockFull) curBlockSize = (int)remaining;
                    }
                    Array.Copy(hashTable, i * hashSize, expectedHash, 0, hashSize);
                    section.Read(currentBlock, 0, curBlockSize);
                    var actualHash = sha256.ComputeHash(currentBlock, 0, curBlockSize);

                    if (!Util.ArraysEqual(expectedHash, actualHash))
                    {
                        return Validity.Invalid;
                    }
                    logger?.ReportAdd(1);
                }
            }

            return Validity.Valid;
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

        public Pfs0Section Pfs0 { get; set; }
        public RomfsSection Romfs { get; set; }
        public bool IsExefs { get; internal set; }
    }

    public static class NcaExtensions
    {
        public static void ExportSection(this Nca nca, int index, string filename, bool raw = false, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (nca.Sections[index] == null) return;

            var section = nca.OpenSection(index, raw);
            var dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                section.CopyStream(outFile, section.Length, logger);
            }
        }

        public static void ExtractSection(this Nca nca, int index, string outputDir, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (nca.Sections[index] == null) return;

            var section = nca.Sections[index];
            var stream = nca.OpenSection(index, false);

            switch (section.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    var pfs0 = new Pfs(stream);
                    pfs0.Extract(outputDir, logger);
                    break;
                case SectionType.Romfs:
                    var romfs = new Romfs(stream);
                    romfs.Extract(outputDir, logger);
                    break;
                case SectionType.Bktr:
                    break;
            }
        }

        public static string Dump(this Nca nca)
        {
            int colLen = 36;
            var sb = new StringBuilder();
            sb.AppendLine();

            sb.AppendLine("NCA:");
            PrintItem("Magic:", nca.Header.Magic);
            PrintItem("Fixed-Key Signature:", nca.Header.Signature1);
            PrintItem("NPDM Signature:", nca.Header.Signature2);
            PrintItem("Content Size:", $"0x{nca.Header.NcaSize:x12}");
            PrintItem("TitleID:", $"{nca.Header.TitleId:X16}");
            PrintItem("SDK Version:", nca.Header.SdkVersion);
            PrintItem("Distribution type:", nca.Header.Distribution);
            PrintItem("Content Type:", nca.Header.ContentType);
            PrintItem("Master Key Revision:", $"{nca.CryptoType} ({Util.GetKeyRevisionSummary(nca.CryptoType)})");
            PrintItem("Encryption Type:", $"{(nca.HasRightsId ? "Titlekey crypto" : "Standard crypto")}");

            if (nca.HasRightsId)
            {
                PrintItem("Rights ID:", nca.Header.RightsId);
            }
            else
            {
                PrintItem("Key Area Encryption Key:", nca.Header.KaekInd);
                sb.AppendLine("Key Area (Encrypted):");
                for (int i = 0; i < 4; i++)
                {
                    PrintItem($"    Key {i} (Encrypted):", nca.Header.EncryptedKeys[i]);
                }

                sb.AppendLine("Key Area (Decrypted):");
                for (int i = 0; i < 4; i++)
                {
                    PrintItem($"    Key {i} (Decrypted):", nca.DecryptedKeys[i]);
                }
            }

            PrintSections();

            return sb.ToString();

            void PrintSections()
            {
                sb.AppendLine("Sections:");

                for (int i = 0; i < 4; i++)
                {
                    NcaSection sect = nca.Sections[i];
                    if (sect == null) continue;

                    sb.AppendLine($"    Section {i}:");
                    PrintItem("        Offset:", $"0x{sect.Offset:x12}");
                    PrintItem("        Size:", $"0x{sect.Size:x12}");
                    PrintItem("        Partition Type:", sect.IsExefs ? "ExeFS" : sect.Type.ToString());
                    PrintItem("        Section CTR:", sect.Header.Ctr);

                    switch (sect.Type)
                    {
                        case SectionType.Pfs0:
                            PrintPfs0(sect);
                            break;
                        case SectionType.Romfs:
                            PrintRomfs(sect);
                            break;
                        case SectionType.Bktr:
                            break;
                        default:
                            sb.AppendLine("        Unknown/invalid superblock!");
                            break;
                    }
                }
            }

            void PrintPfs0(NcaSection sect)
            {
                var sBlock = sect.Pfs0.Superblock;
                PrintItem($"        Superblock Hash{sect.SuperblockHashValidity.GetValidityString()}:", sBlock.MasterHash);
                sb.AppendLine($"        Hash Table{sect.Pfs0.Validity.GetValidityString()}:");

                PrintItem("            Offset:", $"0x{sBlock.HashTableOffset:x12}");
                PrintItem("            Size:", $"0x{sBlock.HashTableSize:x12}");
                PrintItem("            Block Size:", $"0x{sBlock.BlockSize:x}");
                PrintItem("        PFS0 Offset:", $"0x{sBlock.Pfs0Offset:x12}");
                PrintItem("        PFS0 Size:", $"0x{sBlock.Pfs0Size:x12}");
            }

            void PrintRomfs(NcaSection sect)
            {
                var sBlock = sect.Romfs.Superblock;
                var levels = sect.Romfs.IvfcLevels;

                PrintItem($"        Superblock Hash{sect.SuperblockHashValidity.GetValidityString()}:", sBlock.IvfcHeader.MasterHash);
                PrintItem("        Magic:", sBlock.IvfcHeader.Magic);
                PrintItem("        ID:", $"{sBlock.IvfcHeader.Id:x8}");

                for (int i = 0; i < Romfs.IvfcMaxLevel; i++)
                {
                    var level = levels[i];
                    sb.AppendLine($"        Level {i}{level.HashValidity.GetValidityString()}:");
                    PrintItem("            Data Offset:", $"0x{level.DataOffset:x12}");
                    PrintItem("            Data Size:", $"0x{level.DataSize:x12}");
                    PrintItem("            Hash Offset:", $"0x{level.HashOffset:x12}");
                    PrintItem("            Hash BlockSize:", $"0x{level.HashBlockSize:x8}");
                }

            }

            void PrintItem(string prefix, object data)
            {
                if (data is byte[] byteData)
                {
                    sb.MemDump(prefix.PadRight(colLen), byteData);
                }
                else
                {
                    sb.AppendLine(prefix.PadRight(colLen) + data);
                }
            }
        }

        public static string GetValidityString(this Validity validity)
        {
            switch (validity)
            {
                case Validity.Invalid: return " (FAIL)";
                case Validity.Valid: return " (GOOD)";
                default: return string.Empty;
            }
        }
    }
}
