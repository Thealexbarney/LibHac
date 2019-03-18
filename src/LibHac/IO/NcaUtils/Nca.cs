using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibHac.IO.RomFs;

namespace LibHac.IO.NcaUtils
{
    public class Nca : IDisposable
    {
        private const int HeaderSize = 0xc00;
        private const int HeaderSectorSize = 0x200;

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
                if (keyset.TitleKeks[CryptoType].IsEmpty())
                {
                    MissingKeyName = $"titlekek_{CryptoType:x2}";
                }

                TitleKey = titleKey;
                Crypto.DecryptEcb(keyset.TitleKeks[CryptoType], titleKey, TitleKeyDec, 0x10);
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

        public bool CanOpenSection(NcaSectionType type)
        {
            return CanOpenSection(GetSectionIndexFromType(type));
        }

        private IStorage OpenEncryptedStorage(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            NcaSection sect = Sections[index];
            if (sect == null) throw new ArgumentOutOfRangeException(nameof(index), "Section is empty");

            long offset = sect.Offset;
            long size = sect.Size;

            if (!Util.IsSubRange(offset, size, BaseStorage.GetSize()))
            {
                throw new InvalidDataException(
                    $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{BaseStorage.GetSize():x}).");
            }

            return BaseStorage.Slice(offset, size);
        }

        private IStorage OpenDecryptedStorage(IStorage baseStorage, NcaSection sect)
        {
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

            switch (sect.Header.EncryptionType)
            {
                case NcaEncryptionType.None:
                    return baseStorage;
                case NcaEncryptionType.XTS:
                    throw new NotImplementedException("NCA sections using XTS are not supported");
                case NcaEncryptionType.AesCtr:
                    return new CachedStorage(new Aes128CtrStorage(baseStorage, DecryptedKeys[2], sect.Offset, sect.Header.Ctr, true), 0x4000, 4, true);
                case NcaEncryptionType.AesCtrEx:
                    BktrPatchInfo info = sect.Header.BktrInfo;

                    long bktrOffset = info.RelocationHeader.Offset;
                    long bktrSize = sect.Size - bktrOffset;
                    long dataSize = info.RelocationHeader.Offset;

                    IStorage bucketTreeHeader = new MemoryStorage(sect.Header.BktrInfo.EncryptionHeader.Header);
                    IStorage bucketTreeData = new CachedStorage(new Aes128CtrStorage(baseStorage.Slice(bktrOffset, bktrSize), DecryptedKeys[2], bktrOffset + sect.Offset, sect.Header.Ctr, true), 4, true);

                    IStorage encryptionBucketTreeData = bucketTreeData.Slice(info.EncryptionHeader.Offset - bktrOffset);
                    IStorage decStorage = new Aes128CtrExStorage(baseStorage.Slice(0, dataSize), bucketTreeHeader, encryptionBucketTreeData, DecryptedKeys[2], sect.Offset, sect.Header.Ctr, true);
                    decStorage = new CachedStorage(decStorage, 0x4000, 4, true);

                    return new ConcatenationStorage(new[] { decStorage, bucketTreeData }, true);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IStorage OpenRawStorage(int index)
        {
            IStorage encryptedStorage = OpenEncryptedStorage(index);
            IStorage decryptedStorage = OpenDecryptedStorage(encryptedStorage, Sections[index]);

            return decryptedStorage;
        }

        public IStorage OpenRawStorage(NcaSectionType type)
        {
            return OpenRawStorage(GetSectionIndexFromType(type));
        }

        public IStorage OpenStorage(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage rawStorage = OpenRawStorage(index);

            NcaSection sect = Sections[index];
            NcaFsHeader header = sect.Header;

            // todo don't assume that ctr ex means it's a patch
            if (header.EncryptionType == NcaEncryptionType.AesCtrEx)
            {
                return rawStorage.Slice(0, header.BktrInfo.RelocationHeader.Offset);
            }

            switch (header.HashType)
            {
                case NcaHashType.Sha256:
                    return InitIvfcForPartitionfs(header.Sha256Info, rawStorage, integrityCheckLevel, true);
                case NcaHashType.Ivfc:
                    return new HierarchicalIntegrityVerificationStorage(header.IvfcInfo, new MemoryStorage(header.IvfcInfo.MasterHash), rawStorage,
                        IntegrityStorageType.RomFs, integrityCheckLevel, true);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IStorage OpenStorage(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenStorage(GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public IFileSystem OpenFileSystem(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage storage = OpenStorage(index, integrityCheckLevel);

            switch (Sections[index].Header.Type)
            {
                case SectionType.Pfs0:
                    return new PartitionFileSystem(storage);
                case SectionType.Romfs:
                    return new RomFsFileSystem(storage);
                case SectionType.Bktr:
                    // todo Possibly check if a patch completely replaces the original
                    throw new InvalidOperationException("Cannot open a patched section without the original");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IFileSystem OpenFileSystem(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenFileSystem(GetSectionIndexFromType(type), integrityCheckLevel);
        }

        private int GetSectionIndexFromType(NcaSectionType type)
        {
            ContentType contentType = Header.ContentType;

            switch (type)
            {
                case NcaSectionType.Code when contentType == ContentType.Program: return 0;
                case NcaSectionType.Data when contentType == ContentType.Program: return 1;
                case NcaSectionType.Logo when contentType == ContentType.Program: return 2;
                case NcaSectionType.Data: return 0;
                default: throw new ArgumentOutOfRangeException(nameof(type), "NCA does not contain this section type.");
            }
        }

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

        public bool SectionExists(int index)
        {
            if (index < 0 || index > 3) return false;

            return Sections[index] != null;
        }

        public bool SectionExists(NcaSectionType type)
        {
            return SectionExists(GetSectionIndexFromType(type));
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

            IFileSystem pfs = OpenFileSystem(NcaSectionType.Code, IntegrityCheckLevel.ErrorOnInvalid);

            if (!pfs.FileExists("main.npdm")) return;

            IFile npdmStorage = pfs.OpenFile("main.npdm", OpenMode.Read);

            Npdm = new Npdm.NpdmBinary(npdmStorage.AsStream(), Keyset);

            Header.ValidateNpdmSignature(Npdm.AciD.Rsa2048Modulus);
        }

        public IStorage OpenDecryptedNca()
        {
            var builder = new ConcatenationStorageBuilder();
            builder.Add(OpenHeaderStorage(), 0);

            foreach (NcaSection section in Sections.Where(x => x != null))
            {
                builder.Add(OpenRawStorage(section.SectionNum), section.Offset);
            }

            return builder.Build();
        }

        private NcaHeader DecryptHeader()
        {
            if (Keyset.HeaderKey.IsEmpty())
            {
                throw new MissingKeyException("Unable to decrypt NCA header.", "header_key", KeyType.Common);
            }

            return new NcaHeader(new BinaryReader(OpenHeaderStorage().AsStream()), Keyset);
        }

        public IStorage OpenHeaderStorage()
        {
            long size = HeaderSize;

            // Encrypted portion continues until the first section
            if (Sections.Any(x => x != null))
            {
                size = Sections.Where(x => x != null).Min(x => x.Offset);
            }

            IStorage header = new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(0, size), Keyset.HeaderKey, HeaderSectorSize, true), 1, true);
            int version = ReadHeaderVersion(header);

            if (version == 2)
            {
                header = OpenNca2Header(size);
            }

            return header;
        }

        private int ReadHeaderVersion(IStorage header)
        {
            if (Header != null)
            {
                return Header.Version;
            }
            else
            {
                Span<byte> buf = stackalloc byte[1];
                header.Read(buf, 0x203);
                return buf[0] - '0';
            }
        }

        private IStorage OpenNca2Header(long size)
        {
            var sources = new List<IStorage>();
            sources.Add(new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(0, 0x400), Keyset.HeaderKey, HeaderSectorSize, true), 1, true));

            for (int i = 0x400; i < size; i += HeaderSectorSize)
            {
                sources.Add(new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(i, HeaderSectorSize), Keyset.HeaderKey, HeaderSectorSize, true), 1, true));
            }

            return new ConcatenationStorage(sources, true);
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

            IStorage storage = OpenRawStorage(index);

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
}
