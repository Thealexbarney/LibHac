using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LibHac.Fs.RomFs;

namespace LibHac.Fs.NcaUtils
{
    public class Nca
    {
        private Keyset Keyset { get; }
        public IStorage BaseStorage { get; }

        public NcaHeader Header { get; }

        public Nca(Keyset keyset, IStorage storage)
        {
            Keyset = keyset;
            BaseStorage = storage;
            Header = new NcaHeader(keyset, storage);
        }

        public byte[] GetDecryptedKey(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            int keyRevision = Util.GetMasterKeyRevision(Header.KeyGeneration);
            byte[] keyAreaKey = Keyset.KeyAreaKeys[keyRevision][Header.KeyAreaKeyIndex];

            if (keyAreaKey.IsEmpty())
            {
                string keyName = $"key_area_key_{Keyset.KakNames[Header.KeyAreaKeyIndex]}_{keyRevision:x2}";
                throw new MissingKeyException("Unable to decrypt NCA section.", keyName, KeyType.Common);
            }

            byte[] encryptedKey = Header.GetEncryptedKey(index).ToArray();
            var decryptedKey = new byte[Crypto.Aes128Size];

            Crypto.DecryptEcb(keyAreaKey, encryptedKey, decryptedKey, Crypto.Aes128Size);

            return decryptedKey;
        }

        public byte[] GetDecryptedTitleKey()
        {
            int keyRevision = Util.GetMasterKeyRevision(Header.KeyGeneration);
            byte[] titleKek = Keyset.TitleKeks[keyRevision];

            if (!Keyset.TitleKeys.TryGetValue(Header.RightsId.ToArray(), out byte[] encryptedKey))
            {
                throw new MissingKeyException("Missing NCA title key.", Header.RightsId.ToHexString(), KeyType.Title);
            }

            if (titleKek.IsEmpty())
            {
                string keyName = $"titlekek_{keyRevision:x2}";
                throw new MissingKeyException("Unable to decrypt title key.", keyName, KeyType.Common);
            }

            var decryptedKey = new byte[Crypto.Aes128Size];

            Crypto.DecryptEcb(titleKek, encryptedKey, decryptedKey, Crypto.Aes128Size);

            return decryptedKey;
        }

        internal byte[] GetContentKey(NcaKeyType type)
        {
            return Header.HasRightsId ? GetDecryptedTitleKey() : GetDecryptedKey((int)type);
        }

        public bool CanOpenSection(NcaSectionType type) => CanOpenSection(GetSectionIndexFromType(type));

        public bool CanOpenSection(int index)
        {
            if (!SectionExists(index)) return false;
            if (Header.GetFsHeader(index).EncryptionType == NcaEncryptionType.None) return true;

            int keyRevision = Util.GetMasterKeyRevision(Header.KeyGeneration);

            if (Header.HasRightsId)
            {
                return Keyset.TitleKeys.ContainsKey(Header.RightsId.ToArray()) &&
                       !Keyset.TitleKeks[keyRevision].IsEmpty();
            }

            return !Keyset.KeyAreaKeys[keyRevision][Header.KeyAreaKeyIndex].IsEmpty();
        }

        public bool SectionExists(NcaSectionType type) => SectionExists(GetSectionIndexFromType(type));

        public bool SectionExists(int index)
        {
            return Header.IsSectionEnabled(index);
        }

        private IStorage OpenEncryptedStorage(int index)
        {
            if (!SectionExists(index)) throw new ArgumentException(nameof(index), Messages.NcaSectionMissing);

            long offset = Header.GetSectionStartOffset(index);
            long size = Header.GetSectionSize(index);

            if (!Util.IsSubRange(offset, size, BaseStorage.GetSize()))
            {
                throw new InvalidDataException(
                    $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{BaseStorage.GetSize():x}).");
            }

            return BaseStorage.Slice(offset, size);
        }

        private IStorage OpenDecryptedStorage(IStorage baseStorage, int index)
        {
            NcaFsHeader header = Header.GetFsHeader(index);

            switch (header.EncryptionType)
            {
                case NcaEncryptionType.None:
                    return baseStorage;
                case NcaEncryptionType.XTS:
                    return OpenAesXtsStorage(baseStorage, index);
                case NcaEncryptionType.AesCtr:
                    return OpenAesCtrStorage(baseStorage, index);
                case NcaEncryptionType.AesCtrEx:
                    return OpenAesCtrExStorage(baseStorage, index);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // ReSharper disable once UnusedParameter.Local
        private IStorage OpenAesXtsStorage(IStorage baseStorage, int index)
        {
            throw new NotImplementedException("NCA sections using XTS are not supported yet.");
        }

        private IStorage OpenAesCtrStorage(IStorage baseStorage, int index)
        {
            NcaFsHeader fsHeader = Header.GetFsHeader(index);
            byte[] key = GetContentKey(NcaKeyType.AesCtr);
            byte[] counter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, Header.GetSectionStartOffset(index));

            var aesStorage = new Aes128CtrStorage(baseStorage, key, Header.GetSectionStartOffset(index), counter, true);
            return new CachedStorage(aesStorage, 0x4000, 4, true);
        }

        private IStorage OpenAesCtrExStorage(IStorage baseStorage, int index)
        {
            NcaFsHeader fsHeader = Header.GetFsHeader(index);
            NcaFsPatchInfo info = fsHeader.GetPatchInfo();

            long sectionOffset = Header.GetSectionStartOffset(index);
            long sectionSize = Header.GetSectionSize(index);

            long bktrOffset = info.RelocationTreeOffset;
            long bktrSize = sectionSize - bktrOffset;
            long dataSize = info.RelocationTreeOffset;

            byte[] key = GetContentKey(NcaKeyType.AesCtr);
            byte[] counter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, bktrOffset + sectionOffset);
            byte[] counterEx = Aes128CtrStorage.CreateCounter(fsHeader.Counter, sectionOffset);

            IStorage bucketTreeData = new CachedStorage(new Aes128CtrStorage(baseStorage.Slice(bktrOffset, bktrSize), key, counter, true), 4, true);

            IStorage encryptionBucketTreeData = bucketTreeData.Slice(info.EncryptionTreeOffset - bktrOffset);
            IStorage decStorage = new Aes128CtrExStorage(baseStorage.Slice(0, dataSize), encryptionBucketTreeData, key, counterEx, true);
            decStorage = new CachedStorage(decStorage, 0x4000, 4, true);

            return new ConcatenationStorage(new[] { decStorage, bucketTreeData }, true);
        }

        public IStorage OpenRawStorage(int index)
        {
            IStorage encryptedStorage = OpenEncryptedStorage(index);
            IStorage decryptedStorage = OpenDecryptedStorage(encryptedStorage, index);

            return decryptedStorage;
        }

        public IStorage OpenRawStorageWithPatch(Nca patchNca, int index)
        {
            IStorage patchStorage = patchNca.OpenRawStorage(index);
            IStorage baseStorage = OpenRawStorage(index);

            NcaFsHeader header = patchNca.Header.GetFsHeader(index);
            NcaFsPatchInfo patchInfo = header.GetPatchInfo();

            if (patchInfo.RelocationTreeSize == 0)
            {
                return patchStorage;
            }

            IStorage relocationTableStorage = patchStorage.Slice(patchInfo.RelocationTreeOffset, patchInfo.RelocationTreeSize);

            return new IndirectStorage(relocationTableStorage, true, baseStorage, patchStorage);
        }

        public IStorage OpenStorage(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage rawStorage = OpenRawStorage(index);
            NcaFsHeader header = Header.GetFsHeader(index);

            if (header.EncryptionType == NcaEncryptionType.AesCtrEx)
            {
                return rawStorage.Slice(0, header.GetPatchInfo().RelocationTreeOffset);
            }

            switch (header.HashType)
            {
                case NcaHashType.Sha256:
                    return InitIvfcForPartitionFs(header.GetIntegrityInfoSha256(), rawStorage, integrityCheckLevel, true);
                case NcaHashType.Ivfc:
                    return InitIvfcForRomFs(header.GetIntegrityInfoIvfc(), rawStorage, integrityCheckLevel, true);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IStorage OpenStorageWithPatch(Nca patchNca, int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage rawStorage = OpenRawStorageWithPatch(patchNca, index);
            NcaFsHeader header = patchNca.Header.GetFsHeader(index);

            switch (header.HashType)
            {
                case NcaHashType.Sha256:
                    return InitIvfcForPartitionFs(header.GetIntegrityInfoSha256(), rawStorage, integrityCheckLevel, true);
                case NcaHashType.Ivfc:
                    return InitIvfcForRomFs(header.GetIntegrityInfoIvfc(), rawStorage, integrityCheckLevel, true);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IFileSystem OpenFileSystem(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage storage = OpenStorage(index, integrityCheckLevel);
            NcaFsHeader header = Header.GetFsHeader(index);

            return OpenFileSystem(storage, header);
        }

        public IFileSystem OpenFileSystemWithPatch(Nca patchNca, int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage storage = OpenStorageWithPatch(patchNca, index, integrityCheckLevel);
            NcaFsHeader header = Header.GetFsHeader(index);

            return OpenFileSystem(storage, header);
        }

        private IFileSystem OpenFileSystem(IStorage storage, NcaFsHeader header)
        {
            switch (header.FormatType)
            {
                case NcaFormatType.Pfs0:
                    return new PartitionFileSystem(storage);
                case NcaFormatType.Romfs:
                    return new RomFsFileSystem(storage);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IFileSystem OpenFileSystem(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenFileSystem(GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public IFileSystem OpenFileSystemWithPatch(Nca patchNca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenFileSystemWithPatch(patchNca, GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public IStorage OpenRawStorage(NcaSectionType type)
        {
            return OpenRawStorage(GetSectionIndexFromType(type));
        }

        public IStorage OpenRawStorageWithPatch(Nca patchNca, NcaSectionType type)
        {
            return OpenRawStorageWithPatch(patchNca, GetSectionIndexFromType(type));
        }

        public IStorage OpenStorage(NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenStorage(GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public IStorage OpenStorageWithPatch(Nca patchNca, NcaSectionType type, IntegrityCheckLevel integrityCheckLevel)
        {
            return OpenStorageWithPatch(patchNca, GetSectionIndexFromType(type), integrityCheckLevel);
        }

        public IStorage OpenDecryptedNca()
        {
            var builder = new ConcatenationStorageBuilder();
            builder.Add(OpenDecryptedHeaderStorage(), 0);

            for (int i = 0; i < NcaHeader.SectionCount; i++)
            {
                if (Header.IsSectionEnabled(i))
                {
                    builder.Add(OpenRawStorage(i), Header.GetSectionStartOffset(i));
                }
            }

            return builder.Build();
        }

        private int GetSectionIndexFromType(NcaSectionType type)
        {
            return SectionIndexFromType(type, Header.ContentType);
        }

        public static int SectionIndexFromType(NcaSectionType type, ContentType contentType)
        {
            switch (type)
            {
                case NcaSectionType.Code when contentType == ContentType.Program: return 0;
                case NcaSectionType.Data when contentType == ContentType.Program: return 1;
                case NcaSectionType.Logo when contentType == ContentType.Program: return 2;
                case NcaSectionType.Data: return 0;
                default: throw new ArgumentOutOfRangeException(nameof(type), "NCA does not contain this section type.");
            }
        }

        public static NcaSectionType SectionTypeFromIndex(int index, ContentType contentType)
        {
            switch (index)
            {
                case 0 when contentType == ContentType.Program: return NcaSectionType.Code;
                case 1 when contentType == ContentType.Program: return NcaSectionType.Data;
                case 2 when contentType == ContentType.Program: return NcaSectionType.Logo;
                case 0: return NcaSectionType.Data;
                default: throw new ArgumentOutOfRangeException(nameof(index), "NCA type does not contain this index.");
            }
        }

        private static HierarchicalIntegrityVerificationStorage InitIvfcForPartitionFs(NcaFsIntegrityInfoSha256 info,
            IStorage pfsStorage, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
        {
            Debug.Assert(info.LevelCount == 2);

            IStorage hashStorage = pfsStorage.Slice(info.GetLevelOffset(0), info.GetLevelSize(0), leaveOpen);
            IStorage dataStorage = pfsStorage.Slice(info.GetLevelOffset(1), info.GetLevelSize(1), leaveOpen);

            var initInfo = new IntegrityVerificationInfo[3];

            // Set the master hash
            initInfo[0] = new IntegrityVerificationInfo
            {
                // todo Get hash directly from header
                Data = new MemoryStorage(info.MasterHash.ToArray()),

                BlockSize = 0,
                Type = IntegrityStorageType.PartitionFs
            };

            initInfo[1] = new IntegrityVerificationInfo
            {
                Data = hashStorage,
                BlockSize = (int)info.GetLevelSize(0),
                Type = IntegrityStorageType.PartitionFs
            };

            initInfo[2] = new IntegrityVerificationInfo
            {
                Data = dataStorage,
                BlockSize = info.BlockSize,
                Type = IntegrityStorageType.PartitionFs
            };

            return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel, leaveOpen);
        }

        private static HierarchicalIntegrityVerificationStorage InitIvfcForRomFs(NcaFsIntegrityInfoIvfc ivfc,
            IStorage dataStorage, IntegrityCheckLevel integrityCheckLevel, bool leaveOpen)
        {
            var initInfo = new IntegrityVerificationInfo[ivfc.LevelCount];

            initInfo[0] = new IntegrityVerificationInfo
            {
                Data = new MemoryStorage(ivfc.MasterHash.ToArray()),
                BlockSize = 0
            };

            for (int i = 1; i < ivfc.LevelCount; i++)
            {
                initInfo[i] = new IntegrityVerificationInfo
                {
                    Data = dataStorage.Slice(ivfc.GetLevelOffset(i - 1), ivfc.GetLevelSize(i - 1)),
                    BlockSize = 1 << ivfc.GetLevelBlockSize(i - 1),
                    Type = IntegrityStorageType.RomFs
                };
            }

            return new HierarchicalIntegrityVerificationStorage(initInfo, integrityCheckLevel, leaveOpen);
        }

        public IStorage OpenDecryptedHeaderStorage()
        {
            long firstSectionOffset = long.MaxValue;
            bool hasEnabledSection = false;

            // Encrypted portion continues until the first section
            for (int i = 0; i < NcaHeader.SectionCount; i++)
            {
                if (Header.IsSectionEnabled(i))
                {
                    hasEnabledSection = true;
                    firstSectionOffset = Math.Min(firstSectionOffset, Header.GetSectionStartOffset(i));
                }
            }

            long headerSize = hasEnabledSection ? NcaHeader.HeaderSize : firstSectionOffset;

            IStorage header = new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(0, headerSize), Keyset.HeaderKey, NcaHeader.HeaderSectorSize, true), 1, true);
            int version = ReadHeaderVersion(header);

            if (version == 2)
            {
                header = OpenNca2Header(headerSize);
            }

            return header;
        }

        private int ReadHeaderVersion(IStorage header)
        {
            Span<byte> buf = stackalloc byte[1];
            header.Read(buf, 0x203);
            return buf[0] - '0';
        }

        private IStorage OpenNca2Header(long size)
        {
            const int sectorSize = NcaHeader.HeaderSectorSize;

            var sources = new List<IStorage>();
            sources.Add(new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(0, 0x400), Keyset.HeaderKey, sectorSize, true), 1, true));

            for (int i = 0x400; i < size; i += sectorSize)
            {
                sources.Add(new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(i, sectorSize), Keyset.HeaderKey, sectorSize, true), 1, true));
            }

            return new ConcatenationStorage(sources, true);
        }

        public Validity VerifyHeaderSignature()
        {
            return Header.VerifySignature1(Keyset.NcaHdrFixedKeyModulus);
        }

        internal void GenerateAesCounter(int sectionIndex, CnmtContentType type, int minorVersion)
        {
            int counterType;
            int counterVersion;

            NcaFsHeader header = Header.GetFsHeader(sectionIndex);
            if (header.EncryptionType != NcaEncryptionType.AesCtr &&
                header.EncryptionType != NcaEncryptionType.AesCtrEx) return;

            switch (type)
            {
                case CnmtContentType.Program:
                    counterType = sectionIndex + 1;
                    break;
                case CnmtContentType.HtmlDocument:
                    counterType = (int)CnmtContentType.HtmlDocument;
                    break;
                case CnmtContentType.LegalInformation:
                    counterType = (int)CnmtContentType.LegalInformation;
                    break;
                default:
                    counterType = 0;
                    break;
            }

            // Version of firmware NCAs appears to always be 0
            // Haven't checked delta fragment NCAs
            switch (Header.ContentType)
            {
                case ContentType.Program:
                case ContentType.Manual:
                    counterVersion = Math.Max(minorVersion - 1, 0);
                    break;
                case ContentType.PublicData:
                    counterVersion = minorVersion << 16;
                    break;
                default:
                    counterVersion = 0;
                    break;
            }

            header.CounterType = counterType;
            header.CounterVersion = counterVersion;
        }
    }
}
