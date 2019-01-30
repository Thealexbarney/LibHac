using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.IO;
using LibHac.IO.RomFs;

namespace LibHac
{
    public class Nca : IDisposable
    {
        public NcaHeader Header { get; }
        public string NcaId { get; set; }
        public string Filename { get; set; }
        public bool HasRightsId { get; }
        public int CryptoType { get; }
        public byte[][] DecryptedKeys { get; } = Util.CreateJaggedArray<byte[][]>(4, 0x10);
        public byte[] TitleKey { get; }
        public byte[] TitleKeyDec { get; } = new byte[0x10];
        private bool LeaveOpen { get; }
        private Nca BaseNca { get; set; }
        private IStorage BaseStorage { get; }
        private Keyset Keyset { get; }

        public Npdm.NpdmBinary Npdm { get; private set; }

        private bool IsMissingTitleKey { get; set; }
        private string MissingKeyName { get; set; }

        public NcaSection[] Sections { get; } = new NcaSection[4];

        public Nca(Keyset keyset, IStorage storage, bool leaveOpen)
        {
            LeaveOpen = leaveOpen;
            BaseStorage = storage;
            Keyset = keyset;

            Header = DecryptHeader();

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
        /// Opens the <see cref="IStorage"/> of the underlying NCA file.
        /// </summary>
        /// <returns>The <see cref="IStorage"/> that provides access to the entire raw NCA file.</returns>
        public IStorage GetStorage()
        {
            return BaseStorage.AsReadOnly();
        }

        public bool CanOpenSection(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = Sections[index];
            if (sect == null) return false;

            return sect.Header.EncryptionType == NcaEncryptionType.None || !IsMissingTitleKey && string.IsNullOrWhiteSpace(MissingKeyName);
        }

        private IStorage OpenRawSection(int index, bool leaveOpen)
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

            // todo
            //if (!Util.IsSubRange(offset, size, StreamSource.Length))
            //{
            //    throw new InvalidDataException(
            //        $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{StreamSource.Length:x}).");
            //}

            IStorage rawStorage = BaseStorage.Slice(offset, size, leaveOpen);

            switch (sect.Header.EncryptionType)
            {
                case NcaEncryptionType.None:
                    return rawStorage;
                case NcaEncryptionType.XTS:
                    throw new NotImplementedException("NCA sections using XTS are not supported");
                case NcaEncryptionType.AesCtr:
                    return new CachedStorage(new Aes128CtrStorage(rawStorage, DecryptedKeys[2], offset, sect.Header.Ctr, leaveOpen), 0x4000, 4, leaveOpen);
                case NcaEncryptionType.AesCtrEx:
                    BktrPatchInfo info = sect.Header.BktrInfo;

                    long bktrOffset = info.RelocationHeader.Offset;
                    long bktrSize = size - bktrOffset;
                    long dataSize = info.RelocationHeader.Offset;

                    IStorage bucketTreeHeader = new MemoryStorage(sect.Header.BktrInfo.EncryptionHeader.Header);
                    IStorage bucketTreeData = new CachedStorage(new Aes128CtrStorage(rawStorage.Slice(bktrOffset, bktrSize, leaveOpen), DecryptedKeys[2], bktrOffset + offset, sect.Header.Ctr, leaveOpen), 4, leaveOpen);

                    IStorage encryptionBucketTreeData = bucketTreeData.Slice(info.EncryptionHeader.Offset - bktrOffset);
                    IStorage decStorage = new Aes128CtrExStorage(rawStorage.Slice(0, dataSize, leaveOpen), bucketTreeHeader, encryptionBucketTreeData, DecryptedKeys[2], offset, sect.Header.Ctr, leaveOpen);
                    decStorage = new CachedStorage(decStorage, 0x4000, 4, leaveOpen);

                    return new ConcatenationStorage(new[] { decStorage, bucketTreeData }, leaveOpen);
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
        /// <param name="leaveOpen"><see langword="true"/> to leave the storage open after the <see cref="Nca"/> object is disposed; otherwise, <see langword="false"/>.</param>
        /// <returns>A <see cref="Stream"/> that provides access to the specified section. <see langword="null"/> if the section does not exist.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is outside the valid range.</exception>
        public IStorage OpenSection(int index, bool raw, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
        {
            IStorage rawStorage = OpenRawSection(index, leaveOpen);

            NcaSection sect = Sections[index];
            NcaFsHeader header = sect.Header;

            if (header.EncryptionType == NcaEncryptionType.AesCtrEx)
            {
                if (raw && BaseNca == null) return rawStorage;

                BktrHeader bktrInfo = header.BktrInfo.RelocationHeader;
                IStorage patchStorage = rawStorage.Slice(0, bktrInfo.Offset, leaveOpen);

                if (BaseNca == null) return patchStorage;

                IStorage baseStorage = BaseNca.OpenSection(ProgramPartitionType.Data, true, IntegrityCheckLevel.None, leaveOpen);
                IStorage bktrHeader = new MemoryStorage(bktrInfo.Header);
                IStorage bktrData = rawStorage.Slice(bktrInfo.Offset, bktrInfo.Size, leaveOpen);

                rawStorage = new IndirectStorage(bktrHeader, bktrData, leaveOpen, baseStorage, patchStorage);
            }

            if (raw || rawStorage == null) return rawStorage;

            switch (header.HashType)
            {
                case NcaHashType.Sha256:
                    return InitIvfcForPartitionfs(header.Sha256Info, rawStorage, integrityCheckLevel, leaveOpen);
                case NcaHashType.Ivfc:
                    return new HierarchicalIntegrityVerificationStorage(header.IvfcInfo, new MemoryStorage(header.IvfcInfo.MasterHash), rawStorage,
                        IntegrityStorageType.RomFs, integrityCheckLevel, leaveOpen);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>
        /// Opens one of the sections in the current <see cref="Nca"/> as a <see cref="HierarchicalIntegrityVerificationStorage"/>
        /// Only works with sections that have a <see cref="NcaFsHeader.HashType"/> of <see cref="NcaHashType.Ivfc"/> or <see cref="NcaHashType.Sha256"/>.
        /// </summary>
        /// <param name="index">The index of the NCA section to open. Valid indexes are 0-3.</param>
        /// <param name="integrityCheckLevel">The level of integrity checks to be performed when reading the section.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave the storage open after the <see cref="Nca"/> object is disposed; otherwise, <see langword="false"/>.</param>
        /// <returns>A <see cref="Stream"/> that provides access to the specified section. <see langword="null"/> if the section does not exist,
        /// or is has no hash metadata.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="index"/> is outside the valid range.</exception>
        public HierarchicalIntegrityVerificationStorage OpenHashedSection(int index, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen) =>
            OpenSection(index, false, integrityCheckLevel, leaveOpen) as HierarchicalIntegrityVerificationStorage;

        /// <summary>
        /// Opens one of the sections in the current <see cref="Nca"/>. For use with <see cref="ContentType.Program"/> type NCAs.
        /// </summary>
        /// <param name="type">The type of section to open.</param>
        /// <param name="raw"><see langword="true"/> to open the raw section with hash metadata.</param>
        /// <param name="integrityCheckLevel">The level of integrity checks to be performed when reading the section.
        /// Always <see cref="IntegrityCheckLevel.None"/> if <paramref name="raw"/> is <see langword="false"/>.</param>
        /// <param name="leaveOpen"><see langword="true"/> to leave the storage open after the <see cref="Nca"/> object is disposed; otherwise, <see langword="false"/>.</param>
        /// <returns>A <see cref="Stream"/> that provides access to the specified section. <see langword="null"/> if the section does not exist.</returns>
        /// <exception cref="ArgumentOutOfRangeException">The specified <paramref name="type"/> is outside the valid range.</exception>
        public IStorage OpenSection(ProgramPartitionType type, bool raw, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen) =>
            OpenSection((int)type, raw, integrityCheckLevel, leaveOpen);

        private static HierarchicalIntegrityVerificationStorage InitIvfcForPartitionfs(Sha256Info sb,
            IStorage pfsStorage, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
        {
            IStorage hashStorage = pfsStorage.Slice(sb.HashTableOffset, sb.HashTableSize, leaveOpen);
            IStorage dataStorage = pfsStorage.Slice(sb.DataOffset, sb.DataSize, leaveOpen);

            var initInfo = new IntegrityVerificationInfo[3];

            // Set the master hash
            initInfo[0] = new IntegrityVerificationInfo
            {
                // todo Get hash directly from header
                Data = new MemoryStorage(sb.MasterHash),

                BlockSize = 0,
                Type = IntegrityStorageType.PartitionFs
            };

            initInfo[1] = new IntegrityVerificationInfo
            {
                Data = hashStorage,
                BlockSize = (int)sb.HashTableSize,
                Type = IntegrityStorageType.PartitionFs
            };

            initInfo[2] = new IntegrityVerificationInfo
            {
                Data = dataStorage,
                BlockSize = sb.BlockSize,
                Type = IntegrityStorageType.PartitionFs
            };

            return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel, leaveOpen);
        }

        public IFileSystem OpenSectionFileSystem(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            if (Sections[index] == null) throw new ArgumentOutOfRangeException(nameof(index));
            NcaFsHeader header = Sections[index].Header;

            IStorage storage = OpenSection(index, false, integrityCheckLevel, true);

            switch (header.Type)
            {
                case SectionType.Pfs0:
                    return new PartitionFileSystem(storage);
                case SectionType.Romfs:
                    return new RomFsFileSystem(storage);
                case SectionType.Bktr:
                    return new RomFsFileSystem(storage);
                default:
                    throw new ArgumentOutOfRangeException();
            }
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

        public void ParseNpdm()
        {
            if (Header.ContentType != ContentType.Program) return;

            var pfs = new PartitionFileSystem(OpenSection(ProgramPartitionType.Code, false, IntegrityCheckLevel.ErrorOnInvalid, true));

            if (!pfs.FileExists("main.npdm")) return;

            IStorage npdmStorage = pfs.OpenFile("main.npdm", OpenMode.Read).AsStorage();

            Npdm = new Npdm.NpdmBinary(npdmStorage.AsStream(), Keyset);

            Header.ValidateNpdmSignature(Npdm.AciD.Rsa2048Modulus);
        }

        public IStorage OpenDecryptedNca()
        {
            var list = new List<IStorage> { OpenHeaderStorage() };

            foreach (NcaSection section in Sections.Where(x => x != null).OrderBy(x => x.Offset))
            {
                list.Add(OpenRawSection(section.SectionNum, true));
            }

            return new ConcatenationStorage(list, true);
        }

        private NcaHeader DecryptHeader()
        {
            if (Keyset.HeaderKey.IsEmpty())
            {
                throw new MissingKeyException("Unable to decrypt NCA header.", "header_key", KeyType.Common);
            }

            return new NcaHeader(new BinaryReader(OpenHeaderStorage().AsStream()), Keyset);
        }

        private CachedStorage OpenHeaderStorage()
        {
            int size = 0x4000;

            // Support reading headers that are only 0xC00 bytes long, but still return
            // the entire header if available.
            if (BaseStorage.Length >= 0xC00 && BaseStorage.Length < 0x4000) size = 0xC00;

            return new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(0, size), Keyset.HeaderKey, 0x200, true), 1, true);
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
            using (var streamDec = new CachedStorage(new Aes128CtrStorage(GetStorage().Slice(sect.Offset, sect.Size), DecryptedKeys[2], sect.Offset, sect.Header.Ctr, true), 0x4000, 4, false))
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
                    offset = sect.Header.IvfcInfo.LevelHeaders[0].Offset;
                    size = 1 << sect.Header.IvfcInfo.LevelHeaders[0].BlockSizePower;
                    break;
            }

            IStorage storage = OpenSection(index, true, IntegrityCheckLevel.None, true);

            var hashTable = new byte[size];
            storage.Read(hashTable, offset);

            sect.MasterHashValidity = Crypto.CheckMemoryHashTable(hashTable, expected, 0, hashTable.Length);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseStorage?.Flush();
                BaseNca?.BaseStorage?.Flush();

                if (!LeaveOpen)
                {
                    BaseStorage?.Dispose();
                    BaseNca?.Dispose();
                }

            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
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

            IStorage storage = nca.OpenSection(index, raw, integrityCheckLevel, true);
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
            IStorage storage = nca.OpenSection(index, false, integrityCheckLevel, true);

            switch (section.Type)
            {
                case SectionType.Invalid:
                    break;
                case SectionType.Pfs0:
                    var pfs0 = new PartitionFileSystem(storage);
                    pfs0.Extract(outputDir, logger);
                    break;
                case SectionType.Romfs:
                    var romfs = new RomFsFileSystem(storage);
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

            HierarchicalIntegrityVerificationStorage stream = nca.OpenHashedSection(index, IntegrityCheckLevel.IgnoreOnInvalid, true);
            if (stream == null) return Validity.Unchecked;

            if (!quiet) logger?.LogMessage($"Verifying section {index}...");
            Validity validity = stream.Validate(true, logger);

            if (hashType == NcaHashType.Ivfc)
            {
                stream.SetLevelValidities(sect.Header.IvfcInfo);
            }
            else if (hashType == NcaHashType.Sha256)
            {
                sect.Header.Sha256Info.HashValidity = validity;
            }

            return validity;
        }
    }
}
