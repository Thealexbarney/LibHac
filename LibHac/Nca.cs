using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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
                if (keyset.TitleKeys.TryGetValue(Header.RightsId, out byte[] titleKey))
                {
                    TitleKey = titleKey;
                    Crypto.DecryptEcb(keyset.Titlekeks[CryptoType], titleKey, TitleKeyDec, 0x10);
                    DecryptedKeys[2] = TitleKeyDec;
                }
                else
                {
                    throw new MissingKeyException("A required key is missing.", $"{Header.RightsId.ToHexString()}", KeyType.Title);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                NcaSection section = ParseSection(i);
                if (section == null) continue;
                Sections[i] = section;
                ValidateSuperblockHash(i);
            }

            foreach (NcaSection pfsSection in Sections.Where(x => x != null && x.Type == SectionType.Pfs0))
            {
                Stream sectionStream = OpenSection(pfsSection.SectionNum, false, false);
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

        private Stream OpenRawSection(int index)
        {
            NcaSection sect = Sections[index];
            if (sect == null) throw new ArgumentOutOfRangeException(nameof(index));

            if (sect.SuperblockHashValidity == Validity.Invalid) return null;

            long offset = sect.Offset;
            long size = sect.Size;

            Stream rawStream = StreamSource.CreateStream(offset, size);

            switch (sect.Header.CryptType)
            {
                case SectionCryptType.None:
                    return rawStream;
                case SectionCryptType.XTS:
                    throw new NotImplementedException("NCA sections using XTS are not supported");
                case SectionCryptType.CTR:
                    return new RandomAccessSectorStream(new Aes128CtrStream(rawStream, DecryptedKeys[2], offset, sect.Header.Ctr), false);
                case SectionCryptType.BKTR:
                    rawStream = new RandomAccessSectorStream(
                        new BktrCryptoStream(rawStream, DecryptedKeys[2], 0, size, offset, sect.Header.Ctr, sect.Header.Bktr),
                        false);
                    if (BaseNca == null) return rawStream;

                    NcaSection baseSect = BaseNca.Sections.FirstOrDefault(x => x.Type == SectionType.Romfs);
                    if (baseSect == null) throw new InvalidDataException("Base NCA has no RomFS section");

                    Stream baseStream = BaseNca.OpenSection(baseSect.SectionNum, true, false);
                    return new Bktr(rawStream, baseStream, sect);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public Stream OpenSection(int index, bool raw, bool enableIntegrityChecks)
        {
            Stream rawStream = OpenRawSection(index);
            NcaSection sect = Sections[index];

            if (raw) return rawStream;

            switch (sect.Header.Type)
            {
                case SectionType.Pfs0:
                    PfsSuperblock pfs0Superblock = sect.Pfs0.Superblock;

                    return new SubStream(rawStream, pfs0Superblock.Pfs0Offset, pfs0Superblock.Pfs0Size);
                case SectionType.Romfs:
                case SectionType.Bktr:

                    var romfsStreamSource = new SharedStreamSource(rawStream);

                    IvfcHeader ivfc;
                    if (sect.Header.Type == SectionType.Romfs)
                    {
                        ivfc = sect.Header.Romfs.IvfcHeader;
                    }
                    else
                    {
                        ivfc = sect.Header.Bktr.IvfcHeader;
                    }

                    return InitIvfcForRomfs(ivfc, romfsStreamSource, enableIntegrityChecks);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private static HierarchicalIntegrityVerificationStream InitIvfcForRomfs(IvfcHeader ivfc,
            SharedStreamSource romfsStreamSource, bool enableIntegrityChecks)
        {
            var initInfo = new IntegrityVerificationInfo[ivfc.NumLevels];

            // Set the master hash
            initInfo[0] = new IntegrityVerificationInfo
            {
                Data = new MemoryStream(ivfc.MasterHash),
                BlockSizePower = 0
            };

            for (int i = 1; i < ivfc.NumLevels; i++)
            {
                IvfcLevelHeader level = ivfc.LevelHeaders[i - 1];
                Stream data = romfsStreamSource.CreateStream(level.LogicalOffset, level.HashDataSize);

                initInfo[i] = new IntegrityVerificationInfo
                {
                    Data = data,
                    BlockSizePower = level.BlockSize,
                    Type = IntegrityStreamType.RomFs
                };
            }

            return new HierarchicalIntegrityVerificationStream(initInfo, enableIntegrityChecks);
        }

        public void SetBaseNca(Nca baseNca) => BaseNca = baseNca;

        private void DecryptHeader(Keyset keyset, Stream stream)
        {
            var headerBytes = new byte[0xC00];
            Xts xts = XtsAes128.Create(keyset.HeaderKey);
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
            NcaSectionEntry entry = Header.SectionEntries[index];
            NcaFsHeader header = Header.FsHeaders[index];
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
            IvfcLevelHeader[] headers = sect.Romfs.Superblock.IvfcHeader.LevelHeaders;

            for (int i = 0; i < Romfs.IvfcMaxLevel; i++)
            {
                var level = new IvfcLevel();
                sect.Romfs.IvfcLevels[i] = level;
                IvfcLevelHeader header = headers[i];
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
            long offset = sect.Header.Bktr.SubsectionHeader.Offset;
            using (var streamDec = new RandomAccessSectorStream(new Aes128CtrStream(GetStream(), DecryptedKeys[2], sect.Offset, sect.Size, sect.Offset, sect.Header.Ctr)))
            {
                var reader = new BinaryReader(streamDec);
                streamDec.Position = offset + 8;
                long size = reader.ReadInt64();

                if (size != offset)
                {
                    sect.SuperblockHashValidity = Validity.Invalid;
                }
            }
        }

        private void ValidateSuperblockHash(int index)
        {
            if (Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
            NcaSection sect = Sections[index];

            byte[] expected = null;
            byte[] actual;
            long offset = 0;
            long size = 0;

            switch (sect.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    PfsSuperblock pfs0 = sect.Header.Pfs;
                    expected = pfs0.MasterHash;
                    offset = pfs0.HashTableOffset;
                    size = pfs0.HashTableSize;
                    break;
                case SectionType.Romfs:
                    IvfcHeader ivfc = sect.Header.Romfs.IvfcHeader;
                    expected = ivfc.MasterHash;
                    offset = ivfc.LevelHeaders[0].LogicalOffset;
                    size = 1 << ivfc.LevelHeaders[0].BlockSize;
                    break;
                case SectionType.Bktr:
                    CheckBktrKey(sect);
                    return;
            }

            Stream stream = OpenSection(index, true, false);
            if (stream == null) return;
            if (expected == null) return;

            var hashTable = new byte[size];
            stream.Position = offset;
            stream.Read(hashTable, 0, hashTable.Length);

            using (SHA256 hash = SHA256.Create())
            {
                actual = hash.ComputeHash(hashTable);
            }

            Validity validity = Util.ArraysEqual(expected, actual) ? Validity.Valid : Validity.Invalid;

            sect.SuperblockHashValidity = validity;
            if (sect.Type == SectionType.Romfs) sect.Romfs.IvfcLevels[0].HashValidity = validity;
        }

        public void VerifySection(int index, IProgressReport logger = null)
        {
            if (Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
            NcaSection sect = Sections[index];
            Stream stream = OpenSection(index, true, false);
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
            PfsSuperblock sb = pfs0.Superblock;
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
                IvfcLevel level = levels[i];
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
            long blockCount = Util.DivideByRoundUp(dataLen, blockSize);
            int curBlockSize = (int)blockSize;
            section.Position = dataOffset;
            logger?.SetTotal(blockCount);

            using (SHA256 sha256 = SHA256.Create())
            {
                for (long i = 0; i < blockCount; i++)
                {
                    long remaining = dataLen - i * blockSize;
                    if (remaining < blockSize)
                    {
                        Array.Clear(currentBlock, 0, currentBlock.Length);
                        if (!isFinalBlockFull) curBlockSize = (int)remaining;
                    }
                    Array.Copy(hashTable, i * hashSize, expectedHash, 0, hashSize);
                    section.Read(currentBlock, 0, curBlockSize);
                    byte[] actualHash = sha256.ComputeHash(currentBlock, 0, curBlockSize);

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
        public static void ExportSection(this Nca nca, int index, string filename, bool raw = false, bool verify = false, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (nca.Sections[index] == null) return;

            Stream section = nca.OpenSection(index, raw, verify);
            string dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                section.CopyStream(outFile, section.Length, logger);
            }
        }

        public static void ExtractSection(this Nca nca, int index, string outputDir, bool verify = false, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (nca.Sections[index] == null) return;

            NcaSection section = nca.Sections[index];
            Stream stream = nca.OpenSection(index, false, verify);

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
    }
}
