using System;
using System.IO;
using LibHac.IO;

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
        private bool KeepOpen { get; }
        private Nca BaseNca { get; set; }
        private Storage BaseStorage { get; }

        private bool IsMissingTitleKey { get; set; }
        private string MissingKeyName { get; set; }

        public NcaSection[] Sections { get; } = new NcaSection[4];

        public Nca(Keyset keyset, Storage storage, bool keepOpen)
        {
            KeepOpen = keepOpen;
            BaseStorage = storage;
            DecryptHeader(keyset, BaseStorage);

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
            }
        }

        /// <summary>
        /// Opens a <see cref="Storage"/> of the underlying NCA file.
        /// </summary>
        /// <returns>A <see cref="Storage"/> that provides access to the entire raw NCA file.</returns>
        public Storage GetStorage()
        {
            return BaseStorage;
        }

        public bool CanOpenSection(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = Sections[index];
            if (sect == null) return false;

            return sect.Header.EncryptionType == NcaEncryptionType.None || !IsMissingTitleKey && string.IsNullOrWhiteSpace(MissingKeyName);
        }

        private Storage OpenRawSection(int index)
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

            //if (!Util.IsSubRange(offset, size, StreamSource.Length))
            //{
            //    throw new InvalidDataException(
            //        $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{StreamSource.Length:x}).");
            //}

            Storage rawStorage = BaseStorage.Slice(offset, size);

            switch (sect.Header.EncryptionType)
            {
                case NcaEncryptionType.None:
                    return rawStorage;
                case NcaEncryptionType.XTS:
                    throw new NotImplementedException("NCA sections using XTS are not supported");
                case NcaEncryptionType.AesCtr:
                    var counter = new byte[0x10];
                    Array.Copy(sect.Header.Ctr, counter, 8);

                    ulong off = (ulong)offset >> 4;
                    for (uint j = 0; j < 0x7; j++)
                    {
                        counter[0x10 - j - 1] = (byte)(off & 0xFF);
                        off >>= 8;
                    }

                    // Because the value stored in the counter is offset >> 4, the top 4 bits 
                    // of byte 8 need to have their original value preserved
                    counter[8] = (byte)((counter[8] & 0xF0) | (int)(off & 0x0F));

                    return new CachedStorage(new Aes128CtrStorage(rawStorage, DecryptedKeys[2], counter, KeepOpen), 0x4000, 4, false);
                case NcaEncryptionType.AesCtrEx:
                    Storage decStorage = new Aes128CtrExStorage(rawStorage, DecryptedKeys[2], offset, sect.Header.Ctr, sect.Header.BktrInfo, true);
                    decStorage = new CachedStorage(decStorage, 0x4000, 4, true);

                    if (BaseNca == null) return decStorage;

                    Storage baseStorage = BaseNca.OpenSection(ProgramPartitionType.Data, true, IntegrityCheckLevel.None);

                    BktrHeader header = sect.Header.BktrInfo.RelocationHeader;
                    Storage bktrHeader = new MemoryStorage(header.Header);
                    Storage bktrData = decStorage.Slice(header.Offset, header.Size);

                    return new IndirectStorage(bktrHeader, bktrData, baseStorage, decStorage);
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
        public Storage OpenSection(int index, bool raw, IntegrityCheckLevel integrityCheckLevel)
        {
            Storage rawStorage = OpenRawSection(index);

            if (raw || rawStorage == null) return rawStorage;

            NcaSection sect = Sections[index];
            NcaFsHeader header = sect.Header;

            // If it's a patch section without a base, return the raw section because it has no hash data
            if (header.EncryptionType == NcaEncryptionType.AesCtrEx && BaseNca == null) return rawStorage;

            switch (header.HashType)
            {
                case NcaHashType.Sha256:
                    return InitIvfcForPartitionfs(header.Sha256Info, rawStorage, integrityCheckLevel);
                case NcaHashType.Ivfc:
                    return InitIvfcForRomfs(header.IvfcInfo, rawStorage, integrityCheckLevel);

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
        public HierarchicalIntegrityVerificationStorage OpenHashedSection(int index, IntegrityCheckLevel integrityCheckLevel) =>
            OpenSection(index, false, integrityCheckLevel) as HierarchicalIntegrityVerificationStorage;

        /// <summary>
        /// Opens one of the sections in the current <see cref="Nca"/>. For use with <see cref="ContentType.Program"/> type NCAs.
        /// </summary>
        /// <param name="type">The type of section to open.</param>
        /// <param name="raw"><see langword="true"/> to open the raw section with hash metadata.</param>
        /// <param name="integrityCheckLevel">The level of integrity checks to be performed when reading the section.
        /// Always <see cref="IntegrityCheckLevel.None"/> if <paramref name="raw"/> is <see langword="false"/>.</param>
        /// <returns>A <see cref="Stream"/> that provides access to the specified section. <see langword="null"/> if the section does not exist.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="type"/> is outside the valid range.</exception>
        public Storage OpenSection(ProgramPartitionType type, bool raw, IntegrityCheckLevel integrityCheckLevel) =>
            OpenSection((int)type, raw, integrityCheckLevel);

        private static HierarchicalIntegrityVerificationStorage InitIvfcForRomfs(IvfcHeader ivfc,
            Storage romfsStorage, IntegrityCheckLevel integrityCheckLevel)
        {
            var initInfo = new IntegrityVerificationInfoStorage[ivfc.NumLevels];

            // Set the master hash
            initInfo[0] = new IntegrityVerificationInfoStorage
            {
                Data = new StreamStorage(new MemoryStream(ivfc.MasterHash), true),
                BlockSize = 0
            };

            for (int i = 1; i < ivfc.NumLevels; i++)
            {
                IvfcLevelHeader level = ivfc.LevelHeaders[i - 1];
                Storage data = new SubStorage(romfsStorage, level.LogicalOffset, level.HashDataSize);

                initInfo[i] = new IntegrityVerificationInfoStorage
                {
                    Data = data,
                    BlockSize = 1 << level.BlockSizePower,
                    Type = IntegrityStreamType.RomFs
                };
            }

            return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel);
        }

        private static HierarchicalIntegrityVerificationStorage InitIvfcForPartitionfs(Sha256Info sb,
            Storage pfsStorage, IntegrityCheckLevel integrityCheckLevel)
        {
            SubStorage hashStorage = pfsStorage.Slice(sb.HashTableOffset, sb.HashTableSize);
            SubStorage dataStorage = pfsStorage.Slice(sb.DataOffset, sb.DataSize);

            var initInfo = new IntegrityVerificationInfoStorage[3];

            // Set the master hash
            initInfo[0] = new IntegrityVerificationInfoStorage
            {
                Data = new StreamStorage(new MemoryStream(sb.MasterHash), true),

                BlockSize = 0,
                Type = IntegrityStreamType.PartitionFs
            };

            initInfo[1] = new IntegrityVerificationInfoStorage
            {
                Data = hashStorage,
                BlockSize = (int)sb.HashTableSize,
                Type = IntegrityStreamType.PartitionFs
            };

            initInfo[2] = new IntegrityVerificationInfoStorage
            {
                Data = dataStorage,
                BlockSize = sb.BlockSize,
                Type = IntegrityStreamType.PartitionFs
            };

            return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel);
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

        private void DecryptHeader(Keyset keyset, Storage storage)
        {
            if (keyset.HeaderKey.IsEmpty())
            {
                throw new MissingKeyException("Unable to decrypt NCA header.", "header_key", KeyType.Common);
            }

            var headerStorage = new XtsStorage(new SubStorage(storage, 0, 0xC00), keyset.HeaderKey, 0x200);

            var reader = new BinaryReader(headerStorage.AsStream());

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
            using (var streamDec = new CachedStorage(new Aes128CtrStorage(GetStorage().Slice(sect.Offset, sect.Size), DecryptedKeys[2], sect.Offset, true, sect.Header.Ctr), 0x4000, 4, false))
            {
                var reader = new BinaryReader(streamDec.AsStream());
                reader.BaseStream.Position = offset + 8;
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

            switch (sect.Header.HashType)
            {
                case NcaHashType.Sha256:
                    offset = sect.Header.Sha256Info.HashTableOffset;
                    size = sect.Header.Sha256Info.HashTableSize;
                    break;
                case NcaHashType.Ivfc when sect.Header.EncryptionType == NcaEncryptionType.AesCtrEx:
                    CheckBktrKey(sect);
                    return;
                case NcaHashType.Ivfc:
                    offset = sect.Header.IvfcInfo.LevelHeaders[0].LogicalOffset;
                    size = 1 << sect.Header.IvfcInfo.LevelHeaders[0].BlockSizePower;
                    break;
            }

            Storage storage = OpenSection(index, true, IntegrityCheckLevel.None);

            var hashTable = new byte[size];
            storage.Read(hashTable, offset);

            sect.MasterHashValidity = Crypto.CheckMemoryHashTable(hashTable, expected, 0, hashTable.Length);
        }

        public void Dispose()
        {
            if (!KeepOpen)
            {
                BaseStorage?.Dispose();
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

        public Validity MasterHashValidity
        {
            get
            {
                if (Header.HashType == NcaHashType.Ivfc) return Header.IvfcInfo.LevelHeaders[0].HashValidity;
                if (Header.HashType == NcaHashType.Sha256) return Header.Sha256Info.MasterHashValidity;
                return Validity.Unchecked;
            }
            set
            {
                if (Header.HashType == NcaHashType.Ivfc) Header.IvfcInfo.LevelHeaders[0].HashValidity = value;
                if (Header.HashType == NcaHashType.Sha256) Header.Sha256Info.MasterHashValidity = value;
            }
        }

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

            Storage storage = nca.OpenSection(index, raw, integrityCheckLevel);
            string dir = Path.GetDirectoryName(filename);
            if (!string.IsNullOrWhiteSpace(dir)) Directory.CreateDirectory(dir);

            using (var outFile = new FileStream(filename, FileMode.Create, FileAccess.ReadWrite))
            {
                storage.CopyToStream(outFile, storage.Length, logger);
            }
        }

        public static void ExtractSection(this Nca nca, int index, string outputDir, IntegrityCheckLevel integrityCheckLevel = IntegrityCheckLevel.None, IProgressReport logger = null)
        {
            if (index < 0 || index > 3) throw new IndexOutOfRangeException();
            if (nca.Sections[index] == null) return;

            NcaSection section = nca.Sections[index];
            Storage storage = nca.OpenSection(index, false, integrityCheckLevel);

            switch (section.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    var pfs0 = new Pfs(storage);
                    pfs0.Extract(outputDir, logger);
                    break;
                case SectionType.Romfs:
                    var romfs = new Romfs(storage);
                    romfs.Extract(outputDir, logger);
                    break;
                case SectionType.Bktr:
                    break;
            }
        }

        public static Validity VerifyNca(this Nca nca, IProgressReport logger = null, bool quiet = false)
        {
            for (int i = 0; i < 3; i++)
            {
                if (nca.Sections[i] != null)
                {
                    Validity sectionValidity = VerifySection(nca, i, logger, quiet);

                    if (sectionValidity == Validity.Invalid) return Validity.Invalid;
                }
            }

            return Validity.Valid;
        }

        public static Validity VerifySection(this Nca nca, int index, IProgressReport logger = null, bool quiet = false)
        {
            if (nca.Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = nca.Sections[index];
            NcaHashType hashType = sect.Header.HashType;
            if (hashType != NcaHashType.Sha256 && hashType != NcaHashType.Ivfc) return Validity.Unchecked;

            HierarchicalIntegrityVerificationStorage stream = nca.OpenHashedSection(index, IntegrityCheckLevel.IgnoreOnInvalid);
            if (stream == null) return Validity.Unchecked;

            return Validity.Unchecked;
            // todo
            //if (!quiet) logger?.LogMessage($"Verifying section {index}...");
            //Validity validity = stream.Validate(true, logger);

            //if (hashType == NcaHashType.Ivfc)
            //{
            //    stream.SetLevelValidities(sect.Header.IvfcInfo);
            //}
            //else if (hashType == NcaHashType.Sha256)
            //{
            //    sect.Header.Sha256Info.HashValidity = validity;
            //}

            //return validity;
        }
    }
}
