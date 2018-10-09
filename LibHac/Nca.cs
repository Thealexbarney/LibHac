using System;
using System.IO;
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
        private SharedStreamSource StreamSource { get; }
        private bool KeepOpen { get; }
        private Nca BaseNca { get; set; }

        private bool IsMissingTitleKey { get; set; }
        private string MissingKeyName { get; set; }

        public NcaSection[] Sections { get; } = new NcaSection[4];

        public Nca(Keyset keyset, Stream stream, bool keepOpen)
        {
            stream.Position = 0;
            KeepOpen = keepOpen;
            StreamSource = new SharedStreamSource(stream, keepOpen);
            DecryptHeader(keyset, stream);

            CryptoType = Math.Max(Header.CryptoType, Header.CryptoType2);
            if (CryptoType > 0) CryptoType--;

            HasRightsId = !Header.RightsId.IsEmpty();

            if (!HasRightsId)
            {
                DecryptKeyArea(keyset);
            }
            else if (keyset.TitleKeys.TryGetValue(Header.RightsId, out byte[] titleKey))
            {
                if (keyset.Titlekeks[CryptoType].IsEmpty())
                {
                    MissingKeyName = $"titlekek_{CryptoType:x2}";
                }

                TitleKey = titleKey;
                Crypto.DecryptEcb(keyset.Titlekeks[CryptoType], titleKey, TitleKeyDec, 0x10);
                DecryptedKeys[2] = TitleKeyDec;
            }
            else
            {
                IsMissingTitleKey = true;
            }

            for (int i = 0; i < 4; i++)
            {
                NcaSection section = ParseSection(i);
                if (section == null) continue;
                Sections[i] = section;
                ValidateMasterHash(i);
            }

            //foreach (NcaSection pfsSection in Sections.Where(x => x != null && x.Type == SectionType.Pfs0))
            //{
            //    Stream sectionStream = OpenSection(pfsSection.SectionNum, false, false);
            //    if (sectionStream == null) continue;

            //    var pfs = new Pfs(sectionStream);
            //    if (!pfs.FileExists("main.npdm")) continue;

            //    pfsSection.IsExefs = true;
            //}
        }

        /// <summary>
        /// Opens a <see cref="Stream"/> of the underlying NCA file.
        /// </summary>
        /// <returns>A <see cref="Stream"/> that provides access to the entire raw NCA file.</returns>
        public Stream GetStream()
        {
            return StreamSource.CreateStream();
        }

        public bool CanOpenSection(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = Sections[index];
            if (sect == null) return false;

            return sect.Header.EncryptionType == NcaEncryptionType.None || !IsMissingTitleKey && string.IsNullOrWhiteSpace(MissingKeyName);
        }

        private Stream OpenRawSection(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = Sections[index];
            if (sect == null) return null;

            if (sect.Header.EncryptionType != NcaEncryptionType.None)
            {
                if (IsMissingTitleKey)
                {
                    throw new MissingKeyException("Unable to decrypt NCA section.", Header.RightsId.ToHexString(), KeyType.Title);
                }

                if (!string.IsNullOrWhiteSpace(MissingKeyName))
                {
                    throw new MissingKeyException("Unable to decrypt NCA section.", MissingKeyName, KeyType.Common);
                }
            }

            long offset = sect.Offset;
            long size = sect.Size;

            if (!Util.IsSubRange(offset, size, StreamSource.Length))
            {
                throw new InvalidDataException(
                    $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{StreamSource.Length:x}).");
            }

            Stream rawStream = StreamSource.CreateStream(offset, size);

            switch (sect.Header.EncryptionType)
            {
                case NcaEncryptionType.None:
                    return rawStream;
                case NcaEncryptionType.XTS:
                    throw new NotImplementedException("NCA sections using XTS are not supported");
                case NcaEncryptionType.AesCtr:
                    return new RandomAccessSectorStream(new Aes128CtrStream(rawStream, DecryptedKeys[2], offset, sect.Header.Ctr), false);
                case NcaEncryptionType.AesCtrEx:
                    rawStream = new RandomAccessSectorStream(
                        new BktrCryptoStream(rawStream, DecryptedKeys[2], 0, size, offset, sect.Header.Ctr, sect.Header.BktrInfo),
                        false);
                    if (BaseNca == null) return rawStream;

                    Stream baseStream = BaseNca.OpenSection(ProgramPartitionType.Data, true, IntegrityCheckLevel.None);
                    if (baseStream == null) throw new InvalidDataException("Base NCA has no RomFS section");

                    return new Bktr(rawStream, baseStream, sect);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Opens one of the sections in the current <see cref="Nca"/>.
        /// </summary>
        /// <param name="index">The index of the NCA section to open. Valid indexes are 0-3.</param>
        /// <param name="raw"><see langword="true"/> to open the raw section with hash metadata.</param>
        /// <param name="integrityCheckLevel">The level of integrity checks to be performed when reading the section.
        /// Always <see cref="IntegrityCheckLevel.None"/> if <paramref name="raw"/> is <see langword="false"/>.</param>
        /// <returns>A <see cref="Stream"/> that provides access to the specified section. <see langword="null"/> if the section does not exist.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is outside the valid range.</exception>
        public Stream OpenSection(int index, bool raw, IntegrityCheckLevel integrityCheckLevel)
        {
            Stream rawStream = OpenRawSection(index);
            NcaSection sect = Sections[index];
            NcaFsHeader header = sect.Header;

            if (raw || rawStream == null) return rawStream;

            // If it's a patch section without a base, return the raw section because it has no hash data
            if (header.EncryptionType == NcaEncryptionType.AesCtrEx && BaseNca == null) return rawStream;

            switch (header.HashType)
            {
                case NcaHashType.Sha256:
                    return InitIvfcForPartitionfs(header.Sha256Info, new SharedStreamSource(rawStream), integrityCheckLevel);
                case NcaHashType.Ivfc:
                    return InitIvfcForRomfs(header.IvfcInfo, new SharedStreamSource(rawStream), integrityCheckLevel);

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Opens one of the sections in the current <see cref="Nca"/> as a <see cref="HierarchicalIntegrityVerificationStream"/>
        /// Only works with sections that have a <see cref="NcaFsHeader.HashType"/> of <see cref="NcaHashType.Ivfc"/> or <see cref="NcaHashType.Sha256"/>.
        /// </summary>
        /// <param name="index">The index of the NCA section to open. Valid indexes are 0-3.</param>
        /// <param name="integrityCheckLevel">The level of integrity checks to be performed when reading the section.</param>
        /// <returns>A <see cref="Stream"/> that provides access to the specified section. <see langword="null"/> if the section does not exist,
        /// or is has no hash metadata.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is outside the valid range.</exception>
        public HierarchicalIntegrityVerificationStream OpenHashedSection(int index, IntegrityCheckLevel integrityCheckLevel) =>
            OpenSection(index, false, integrityCheckLevel) as HierarchicalIntegrityVerificationStream;

        /// <summary>
        /// Opens one of the sections in the current <see cref="Nca"/>. For use with <see cref="ContentType.Program"/> type NCAs.
        /// </summary>
        /// <param name="type">The type of section to open.</param>
        /// <param name="raw"><see langword="true"/> to open the raw section with hash metadata.</param>
        /// <param name="integrityCheckLevel">The level of integrity checks to be performed when reading the section.
        /// Always <see cref="IntegrityCheckLevel.None"/> if <paramref name="raw"/> is <see langword="false"/>.</param>
        /// <returns>A <see cref="Stream"/> that provides access to the specified section. <see langword="null"/> if the section does not exist.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="type"/> is outside the valid range.</exception>
        public Stream OpenSection(ProgramPartitionType type, bool raw, IntegrityCheckLevel integrityCheckLevel) =>
            OpenSection((int)type, raw, integrityCheckLevel);

        private static HierarchicalIntegrityVerificationStream InitIvfcForRomfs(IvfcHeader ivfc,
            SharedStreamSource romfsStreamSource, IntegrityCheckLevel integrityCheckLevel)
        {
            var initInfo = new IntegrityVerificationInfo[ivfc.NumLevels];

            // Set the master hash
            initInfo[0] = new IntegrityVerificationInfo
            {
                Data = new MemoryStream(ivfc.MasterHash),
                BlockSize = 0
            };

            for (int i = 1; i < ivfc.NumLevels; i++)
            {
                IvfcLevelHeader level = ivfc.LevelHeaders[i - 1];
                Stream data = romfsStreamSource.CreateStream(level.LogicalOffset, level.HashDataSize);

                initInfo[i] = new IntegrityVerificationInfo
                {
                    Data = data,
                    BlockSize = 1 << level.BlockSizePower,
                    Type = IntegrityStreamType.RomFs
                };
            }

            return new HierarchicalIntegrityVerificationStream(initInfo, integrityCheckLevel);
        }

        private static Stream InitIvfcForPartitionfs(Sha256Info sb,
            SharedStreamSource pfsStreamSource, IntegrityCheckLevel integrityCheckLevel)
        {
            SharedStream hashStream = pfsStreamSource.CreateStream(sb.HashTableOffset, sb.HashTableSize);
            SharedStream dataStream = pfsStreamSource.CreateStream(sb.DataOffset, sb.DataSize);

            var initInfo = new IntegrityVerificationInfo[3];

            // Set the master hash
            initInfo[0] = new IntegrityVerificationInfo
            {
                Data = new MemoryStream(sb.MasterHash),
                BlockSize = 0,
                Type = IntegrityStreamType.PartitionFs
            };

            initInfo[1] = new IntegrityVerificationInfo
            {
                Data = hashStream,
                BlockSize = (int)sb.HashTableSize,
                Type = IntegrityStreamType.PartitionFs
            };

            initInfo[2] = new IntegrityVerificationInfo
            {
                Data = dataStream,
                BlockSize = sb.BlockSize,
                Type = IntegrityStreamType.PartitionFs
            };

            return new HierarchicalIntegrityVerificationStream(initInfo, integrityCheckLevel);
        }

        /// <summary>
        /// Sets a base <see cref="Nca"/> to use when reading patches.
        /// </summary>
        /// <param name="baseNca">The base <see cref="Nca"/></param>
        public void SetBaseNca(Nca baseNca) => BaseNca = baseNca;

        /// <summary>
        /// Validates the master hash and store the result in <see cref="NcaSection.MasterHashValidity"/> for each <see cref="NcaSection"/>.
        /// </summary>
        public void ValidateMasterHashes()
        {
            for (int i = 0; i < 4; i++)
            {
                if (Sections[i] == null) continue;
                ValidateMasterHash(i);
            }
        }

        private void DecryptHeader(Keyset keyset, Stream stream)
        {
            if (keyset.HeaderKey.IsEmpty())
            {
                throw new MissingKeyException("Unable to decrypt NCA header.", "header_key", KeyType.Common);
            }

            var headerBytes = new byte[0xC00];
            Xts xts = XtsAes128.Create(keyset.HeaderKey);
            using (var headerDec = new RandomAccessSectorStream(new XtsSectorStream(stream, xts, 0x200)))
            {
                headerDec.Read(headerBytes, 0, headerBytes.Length);
            }

            var reader = new BinaryReader(new MemoryStream(headerBytes));

            Header = new NcaHeader(reader);
        }

        private void DecryptKeyArea(Keyset keyset)
        {
            if (keyset.KeyAreaKeys[CryptoType][Header.KaekInd].IsEmpty())
            {
                MissingKeyName = $"key_area_key_{Keyset.KakNames[Header.KaekInd]}_{CryptoType:x2}";
                return;
            }

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

            return sect;
        }

        private void CheckBktrKey(NcaSection sect)
        {
            // The encryption subsection table in the bktr partition contains the length of the entire partition.
            // The encryption table is always located immediately following the partition data
            // Decrypt this value and compare it to the encryption table offset found in the NCA header

            long offset = sect.Header.BktrInfo.EncryptionHeader.Offset;
            using (var streamDec = new RandomAccessSectorStream(new Aes128CtrStream(GetStream(), DecryptedKeys[2], sect.Offset, sect.Size, sect.Offset, sect.Header.Ctr)))
            {
                var reader = new BinaryReader(streamDec);
                streamDec.Position = offset + 8;
                long size = reader.ReadInt64();

                if (size != offset)
                {
                    sect.MasterHashValidity = Validity.Invalid;
                }
            }
        }

        private void ValidateMasterHash(int index)
        {
            if (Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
            NcaSection sect = Sections[index];

            if (!CanOpenSection(index))
            {
                sect.MasterHashValidity = Validity.MissingKey;
                return;
            }

            byte[] expected = sect.GetMasterHash();
            long offset = 0;
            long size = 0;

            switch (sect.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    offset = sect.Header.Sha256Info.HashTableOffset;
                    size = sect.Header.Sha256Info.HashTableSize;
                    break;
                case SectionType.Romfs:
                    offset = sect.Header.IvfcInfo.LevelHeaders[0].LogicalOffset;
                    size = 1 << sect.Header.IvfcInfo.LevelHeaders[0].BlockSizePower;
                    break;
                case SectionType.Bktr:
                    CheckBktrKey(sect);
                    return;
            }

            Stream stream = OpenSection(index, true, IntegrityCheckLevel.None);

            var hashTable = new byte[size];
            stream.Position = offset;
            stream.Read(hashTable, 0, hashTable.Length);

            sect.MasterHashValidity = Crypto.CheckMemoryHashTable(hashTable, expected, 0, hashTable.Length);
            if (sect.Type == SectionType.Romfs) sect.Header.IvfcInfo.LevelHeaders[0].HashValidity = sect.MasterHashValidity;
        }

        public void Dispose()
        {
            if (!KeepOpen)
            {
                StreamSource?.Dispose();
            }
        }
    }

    public class NcaSection
    {
        public NcaFsHeader Header { get; set; }
        public SectionType Type { get; set; }
        public int SectionNum { get; set; }
        public long Offset { get; set; }
        public long Size { get; set; }
        public Validity MasterHashValidity { get; set; }

        public bool IsExefs { get; internal set; }

        public byte[] GetMasterHash()
        {
            var hash = new byte[Crypto.Sha256DigestSize];

            switch (Header.HashType)
            {
                case NcaHashType.Sha256:
                    Array.Copy(Header.Sha256Info.MasterHash, hash, Crypto.Sha256DigestSize);
                    break;
                case NcaHashType.Ivfc:
                    Array.Copy(Header.IvfcInfo.MasterHash, hash, Crypto.Sha256DigestSize);
                    break;
            }

            return hash;
        }
    }

    public static class NcaExtensions
    {
        public static void ExportSection(this Nca nca, int index, string filename, bool raw = false, IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (nca.Sections[index] == null) return;

            Stream section = nca.OpenSection(index, raw, integrityCheckLevel);
            string dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                section.CopyStream(outFile, section.Length, logger);
            }
        }

        public static void ExtractSection(this Nca nca, int index, string outputDir, IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (nca.Sections[index] == null) return;

            NcaSection section = nca.Sections[index];
            Stream stream = nca.OpenSection(index, false, integrityCheckLevel);

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

        public static void VerifySection(this Nca nca, int index, IProgressReport logger = null)
        {
            if (nca.Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = nca.Sections[index];
            NcaHashType hashType = sect.Header.HashType;
            if (hashType != NcaHashType.Sha256 && hashType != NcaHashType.Ivfc) return;

            HierarchicalIntegrityVerificationStream stream = nca.OpenHashedSection(index, IntegrityCheckLevel.IgnoreOnInvalid);
            if (stream == null) return;

            logger?.LogMessage($"Verifying section {index}...");

            for (int i = 0; i < stream.Levels.Length - 1; i++)
            {
                logger?.LogMessage($"    Verifying Hash Level {i}...");
                Validity result = stream.ValidateLevel(i, true, logger);

                if (hashType == NcaHashType.Ivfc)
                {
                    sect.Header.IvfcInfo.LevelHeaders[i].HashValidity = result;
                }
                else if (hashType == NcaHashType.Sha256 && i == stream.Levels.Length - 2)
                {
                    sect.Header.Sha256Info.HashValidity = result;
                }
            }
        }
    }
}
