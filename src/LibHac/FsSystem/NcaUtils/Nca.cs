using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using LibHac.Common;
using LibHac.Common.Keys;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.RomFs;
using LibHac.Spl;

namespace LibHac.FsSystem.NcaUtils
{
    public class Nca
    {
        private KeySet KeySet { get; }
        private bool IsEncrypted => Header.IsEncrypted;

        private byte[] Nca0KeyArea { get; set; }
        private IStorage Nca0TransformedBody { get; set; }

        public IStorage BaseStorage { get; }

        public NcaHeader Header { get; }

        public Nca(KeySet keySet, IStorage storage)
        {
            KeySet = keySet;
            BaseStorage = storage;
            Header = new NcaHeader(keySet, storage);
        }

        public byte[] GetDecryptedKey(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            // Handle old NCA0s that use different key area encryption
            if (Header.FormatVersion == NcaVersion.Nca0FixedKey || Header.FormatVersion == NcaVersion.Nca0RsaOaep)
            {
                return GetDecryptedKeyAreaNca0().AsSpan(0x10 * index, 0x10).ToArray();
            }

            int keyRevision = Utilities.GetMasterKeyRevision(Header.KeyGeneration);
            byte[] keyAreaKey = KeySet.KeyAreaKeys[keyRevision][Header.KeyAreaKeyIndex].DataRo.ToArray();

            if (keyAreaKey.IsZeros())
            {
                string keyName = $"key_area_key_{KakNames[Header.KeyAreaKeyIndex]}_{keyRevision:x2}";
                throw new MissingKeyException("Unable to decrypt NCA section.", keyName, KeyType.Common);
            }

            byte[] encryptedKey = Header.GetEncryptedKey(index).ToArray();
            byte[] decryptedKey = new byte[Aes.KeySize128];

            Aes.DecryptEcb128(encryptedKey, decryptedKey, keyAreaKey);

            return decryptedKey;
        }

        private static readonly string[] KakNames = { "application", "ocean", "system" };

        public byte[] GetDecryptedTitleKey()
        {
            int keyRevision = Utilities.GetMasterKeyRevision(Header.KeyGeneration);
            byte[] titleKek = KeySet.TitleKeks[keyRevision].DataRo.ToArray();

            var rightsId = new RightsId(Header.RightsId);

            if (KeySet.ExternalKeySet.Get(rightsId, out AccessKey accessKey).IsFailure())
            {
                throw new MissingKeyException("Missing NCA title key.", rightsId.ToString(), KeyType.Title);
            }

            if (titleKek.IsZeros())
            {
                string keyName = $"titlekek_{keyRevision:x2}";
                throw new MissingKeyException("Unable to decrypt title key.", keyName, KeyType.Common);
            }

            byte[] encryptedKey = accessKey.Value.ToArray();
            byte[] decryptedKey = new byte[Aes.KeySize128];

            Aes.DecryptEcb128(encryptedKey, decryptedKey, titleKek);

            return decryptedKey;
        }

        internal byte[] GetContentKey(NcaKeyType type)
        {
            return Header.HasRightsId ? GetDecryptedTitleKey() : GetDecryptedKey((int)type);
        }

        public bool CanOpenSection(NcaSectionType type)
        {
            if (!TryGetSectionIndexFromType(type, Header.ContentType, out int index))
            {
                return false;
            }

            return CanOpenSection(index);
        }

        public bool CanOpenSection(int index)
        {
            if (!SectionExists(index)) return false;
            if (GetFsHeader(index).EncryptionType == NcaEncryptionType.None) return true;

            int keyRevision = Utilities.GetMasterKeyRevision(Header.KeyGeneration);

            if (Header.HasRightsId)
            {
                return KeySet.ExternalKeySet.Contains(new RightsId(Header.RightsId)) &&
                       !KeySet.TitleKeks[keyRevision].IsZeros();
            }

            return !KeySet.KeyAreaKeys[keyRevision][Header.KeyAreaKeyIndex].IsZeros();
        }

        public bool SectionExists(NcaSectionType type)
        {
            if (!TryGetSectionIndexFromType(type, Header.ContentType, out int index))
            {
                return false;
            }

            return SectionExists(index);
        }

        public bool SectionExists(int index)
        {
            return Header.IsSectionEnabled(index);
        }

        public NcaFsHeader GetFsHeader(int index)
        {
            if (Header.IsNca0())
                return GetNca0FsHeader(index);

            return Header.GetFsHeader(index);
        }

        private IStorage OpenSectionStorage(int index)
        {
            if (!SectionExists(index)) throw new ArgumentException(nameof(index), Messages.NcaSectionMissing);

            long offset = Header.GetSectionStartOffset(index);
            long size = Header.GetSectionSize(index);

            BaseStorage.GetSize(out long baseSize).ThrowIfFailure();

            if (!IsSubRange(offset, size, baseSize))
            {
                throw new InvalidDataException(
                    $"Section offset (0x{offset:x}) and length (0x{size:x}) fall outside the total NCA length (0x{baseSize:x}).");
            }

            return BaseStorage.Slice(offset, size);
        }

        private IStorage OpenDecryptedStorage(IStorage baseStorage, int index, bool decrypting)
        {
            NcaFsHeader header = GetFsHeader(index);

            switch (header.EncryptionType)
            {
                case NcaEncryptionType.None:
                    return baseStorage;
                case NcaEncryptionType.XTS:
                    return OpenAesXtsStorage(baseStorage, index, decrypting);
                case NcaEncryptionType.AesCtr:
                    return OpenAesCtrStorage(baseStorage, index);
                case NcaEncryptionType.AesCtrEx:
                    return OpenAesCtrExStorage(baseStorage, index, decrypting);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        // ReSharper disable UnusedParameter.Local
        private IStorage OpenAesXtsStorage(IStorage baseStorage, int index, bool decrypting)
        {
            const int sectorSize = 0x200;

            byte[] key0 = GetContentKey(NcaKeyType.AesXts0);
            byte[] key1 = GetContentKey(NcaKeyType.AesXts1);

            // todo: Handle xts for nca version 3
            return new CachedStorage(new Aes128XtsStorage(baseStorage, key0, key1, sectorSize, true, decrypting), 2, true);
        }
        // ReSharper restore UnusedParameter.Local

        private IStorage OpenAesCtrStorage(IStorage baseStorage, int index)
        {
            NcaFsHeader fsHeader = GetFsHeader(index);
            byte[] key = GetContentKey(NcaKeyType.AesCtr);
            byte[] counter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, Header.GetSectionStartOffset(index));

            var aesStorage = new Aes128CtrStorage(baseStorage, key, Header.GetSectionStartOffset(index), counter, true);
            return new CachedStorage(aesStorage, 0x4000, 4, true);
        }

        private IStorage OpenAesCtrExStorage(IStorage baseStorage, int index, bool decrypting)
        {
            NcaFsHeader fsHeader = GetFsHeader(index);
            NcaFsPatchInfo info = fsHeader.GetPatchInfo();

            long sectionOffset = Header.GetSectionStartOffset(index);
            long sectionSize = Header.GetSectionSize(index);

            long bktrOffset = info.RelocationTreeOffset;
            long bktrSize = sectionSize - bktrOffset;
            long dataSize = info.RelocationTreeOffset;

            byte[] key = GetContentKey(NcaKeyType.AesCtr);
            byte[] counter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, bktrOffset + sectionOffset);
            byte[] counterEx = Aes128CtrStorage.CreateCounter(fsHeader.Counter, sectionOffset);

            IStorage bucketTreeData;
            IStorage outputBucketTreeData;

            if (decrypting)
            {
                bucketTreeData = new CachedStorage(new Aes128CtrStorage(baseStorage.Slice(bktrOffset, bktrSize), key, counter, true), 4, true);
                outputBucketTreeData = bucketTreeData;
            }
            else
            {
                bucketTreeData = baseStorage.Slice(bktrOffset, bktrSize);
                outputBucketTreeData = new CachedStorage(new Aes128CtrStorage(baseStorage.Slice(bktrOffset, bktrSize), key, counter, true), 4, true);
            }

            var encryptionBucketTreeData = new SubStorage(bucketTreeData,
                info.EncryptionTreeOffset - bktrOffset, sectionSize - info.EncryptionTreeOffset);

            var cachedBucketTreeData = new CachedStorage(encryptionBucketTreeData, IndirectStorage.NodeSize, 6, true);

            var treeHeader = new BucketTree.Header();
            info.EncryptionTreeHeader.CopyTo(SpanHelpers.AsByteSpan(ref treeHeader));
            long nodeStorageSize = IndirectStorage.QueryNodeStorageSize(treeHeader.EntryCount);
            long entryStorageSize = IndirectStorage.QueryEntryStorageSize(treeHeader.EntryCount);

            var tableNodeStorage = new SubStorage(cachedBucketTreeData, 0, nodeStorageSize);
            var tableEntryStorage = new SubStorage(cachedBucketTreeData, nodeStorageSize, entryStorageSize);

            IStorage decStorage = new Aes128CtrExStorage(baseStorage.Slice(0, dataSize), tableNodeStorage,
                tableEntryStorage, treeHeader.EntryCount, key, counterEx, true);

            return new ConcatenationStorage(new[] { decStorage, outputBucketTreeData }, true);
        }

        public IStorage OpenRawStorage(int index, bool openEncrypted)
        {
            if (Header.IsNca0())
                return OpenNca0RawStorage(index, openEncrypted);

            IStorage storage = OpenSectionStorage(index);

            if (IsEncrypted == openEncrypted)
            {
                return storage;
            }

            IStorage decryptedStorage = OpenDecryptedStorage(storage, index, !openEncrypted);

            return decryptedStorage;
        }

        public IStorage OpenRawStorage(int index) => OpenRawStorage(index, false);

        public IStorage OpenRawStorageWithPatch(Nca patchNca, int index)
        {
            IStorage patchStorage = patchNca.OpenRawStorage(index);
            IStorage baseStorage = SectionExists(index) ? OpenRawStorage(index) : new NullStorage();

            patchStorage.GetSize(out long patchSize).ThrowIfFailure();
            baseStorage.GetSize(out long baseSize).ThrowIfFailure();

            NcaFsHeader header = patchNca.GetFsHeader(index);
            NcaFsPatchInfo patchInfo = header.GetPatchInfo();

            if (patchInfo.RelocationTreeSize == 0)
            {
                return patchStorage;
            }

            var treeHeader = new BucketTree.Header();
            patchInfo.RelocationTreeHeader.CopyTo(SpanHelpers.AsByteSpan(ref treeHeader));
            long nodeStorageSize = IndirectStorage.QueryNodeStorageSize(treeHeader.EntryCount);
            long entryStorageSize = IndirectStorage.QueryEntryStorageSize(treeHeader.EntryCount);

            var relocationTableStorage = new SubStorage(patchStorage, patchInfo.RelocationTreeOffset, patchInfo.RelocationTreeSize);
            var cachedTableStorage = new CachedStorage(relocationTableStorage, IndirectStorage.NodeSize, 4, true);

            var tableNodeStorage = new SubStorage(cachedTableStorage, 0, nodeStorageSize);
            var tableEntryStorage = new SubStorage(cachedTableStorage, nodeStorageSize, entryStorageSize);

            var storage = new IndirectStorage();
            storage.Initialize(tableNodeStorage, tableEntryStorage, treeHeader.EntryCount).ThrowIfFailure();

            storage.SetStorage(0, baseStorage, 0, baseSize);
            storage.SetStorage(1, patchStorage, 0, patchSize);

            return storage;
        }

        public IStorage OpenStorage(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage rawStorage = OpenRawStorage(index);
            NcaFsHeader header = GetFsHeader(index);

            if (header.EncryptionType == NcaEncryptionType.AesCtrEx)
            {
                return rawStorage.Slice(0, header.GetPatchInfo().RelocationTreeOffset);
            }

            return CreateVerificationStorage(integrityCheckLevel, header, rawStorage);
        }

        public IStorage OpenStorageWithPatch(Nca patchNca, int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage rawStorage = OpenRawStorageWithPatch(patchNca, index);
            NcaFsHeader header = patchNca.GetFsHeader(index);

            return CreateVerificationStorage(integrityCheckLevel, header, rawStorage);
        }

        private IStorage CreateVerificationStorage(IntegrityCheckLevel integrityCheckLevel, NcaFsHeader header,
            IStorage rawStorage)
        {
            switch (header.HashType)
            {
                case NcaHashType.Sha256:
                    return InitIvfcForPartitionFs(header.GetIntegrityInfoSha256(), rawStorage, integrityCheckLevel,
                        true);
                case NcaHashType.Ivfc:
                    // The FS header of an NCA0 section with IVFC verification must be manually skipped
                    if (Header.IsNca0())
                    {
                        rawStorage = rawStorage.Slice(0x200);
                    }

                    return InitIvfcForRomFs(header.GetIntegrityInfoIvfc(), rawStorage, integrityCheckLevel, true);
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public IFileSystem OpenFileSystem(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage storage = OpenStorage(index, integrityCheckLevel);
            NcaFsHeader header = GetFsHeader(index);

            return OpenFileSystem(storage, header);
        }

        public IFileSystem OpenFileSystemWithPatch(Nca patchNca, int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage storage = OpenStorageWithPatch(patchNca, index, integrityCheckLevel);
            NcaFsHeader header = patchNca.GetFsHeader(index);

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

        public IStorage OpenEncryptedNca() => OpenFullNca(true);
        public IStorage OpenDecryptedNca() => OpenFullNca(false);

        public IStorage OpenFullNca(bool openEncrypted)
        {
            if (openEncrypted == IsEncrypted)
            {
                return BaseStorage;
            }

            var builder = new ConcatenationStorageBuilder();
            builder.Add(OpenHeaderStorage(openEncrypted), 0);

            if (Header.IsNca0())
            {
                builder.Add(OpenNca0BodyStorage(openEncrypted), 0x400);
                return builder.Build();
            }

            for (int i = 0; i < NcaHeader.SectionCount; i++)
            {
                if (Header.IsSectionEnabled(i))
                {
                    builder.Add(OpenRawStorage(i, openEncrypted), Header.GetSectionStartOffset(i));
                }
            }

            return builder.Build();
        }

        private int GetSectionIndexFromType(NcaSectionType type)
        {
            return GetSectionIndexFromType(type, Header.ContentType);
        }

        public static int GetSectionIndexFromType(NcaSectionType type, NcaContentType contentType)
        {
            if (!TryGetSectionIndexFromType(type, contentType, out int index))
            {
                throw new ArgumentOutOfRangeException(nameof(type), "NCA does not contain this section type.");
            }

            return index;
        }

        public static bool TryGetSectionIndexFromType(NcaSectionType type, NcaContentType contentType, out int index)
        {
            switch (type)
            {
                case NcaSectionType.Code when contentType == NcaContentType.Program:
                    index = 0;
                    return true;
                case NcaSectionType.Data when contentType == NcaContentType.Program:
                    index = 1;
                    return true;
                case NcaSectionType.Logo when contentType == NcaContentType.Program:
                    index = 2;
                    return true;
                case NcaSectionType.Data:
                    index = 0;
                    return true;
                default:
                    index = 0;
                    return false;
            }
        }

        public static NcaSectionType GetSectionTypeFromIndex(int index, NcaContentType contentType)
        {
            if (!TryGetSectionTypeFromIndex(index, contentType, out NcaSectionType type))
            {
                throw new ArgumentOutOfRangeException(nameof(type), "NCA type does not contain this index.");
            }

            return type;
        }

        public static bool TryGetSectionTypeFromIndex(int index, NcaContentType contentType, out NcaSectionType type)
        {
            switch (index)
            {
                case 0 when contentType == NcaContentType.Program:
                    type = NcaSectionType.Code;
                    return true;
                case 1 when contentType == NcaContentType.Program:
                    type = NcaSectionType.Data;
                    return true;
                case 2 when contentType == NcaContentType.Program:
                    type = NcaSectionType.Logo;
                    return true;
                case 0:
                    type = NcaSectionType.Data;
                    return true;
                default:
                    UnsafeHelpers.SkipParamInit(out type);
                    return false;
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

        public IStorage OpenDecryptedHeaderStorage() => OpenHeaderStorage(false);

        public IStorage OpenHeaderStorage(bool openEncrypted)
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

            long headerSize = hasEnabledSection ? firstSectionOffset : NcaHeader.HeaderSize;
            IStorage rawHeaderStorage = BaseStorage.Slice(0, headerSize);

            if (openEncrypted == IsEncrypted)
                return rawHeaderStorage;

            IStorage header;

            switch (Header.Version)
            {
                case 3:
                    header = new CachedStorage(new Aes128XtsStorage(rawHeaderStorage, KeySet.HeaderKey, NcaHeader.HeaderSectorSize, true, !openEncrypted), 1, true);
                    break;
                case 2:
                    header = OpenNca2Header(headerSize, !openEncrypted);
                    break;
                case 0:
                    header = new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(0, 0x400), KeySet.HeaderKey, NcaHeader.HeaderSectorSize, true, !openEncrypted), 1, true);
                    break;
                default:
                    throw new NotSupportedException("Unsupported NCA version");
            }

            return header;
        }

        private IStorage OpenNca2Header(long size, bool decrypting)
        {
            const int sectorSize = NcaHeader.HeaderSectorSize;

            var sources = new List<IStorage>();
            sources.Add(new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(0, 0x400), KeySet.HeaderKey, sectorSize, true, decrypting), 1, true));

            for (int i = 0x400; i < size; i += sectorSize)
            {
                sources.Add(new CachedStorage(new Aes128XtsStorage(BaseStorage.Slice(i, sectorSize), KeySet.HeaderKey, sectorSize, true, decrypting), 1, true));
            }

            return new ConcatenationStorage(sources, true);
        }

        private byte[] GetDecryptedKeyAreaNca0()
        {
            if (Nca0KeyArea != null)
                return Nca0KeyArea;

            if (Header.FormatVersion == NcaVersion.Nca0FixedKey)
            {
                Nca0KeyArea = Header.GetKeyArea().ToArray();
            }
            else if (Header.FormatVersion == NcaVersion.Nca0RsaOaep)
            {
                Span<byte> keyArea = Header.GetKeyArea();
                byte[] decKeyArea = new byte[0x100];

                if (CryptoOld.DecryptRsaOaep(keyArea, decKeyArea, KeySet.BetaNca0KeyAreaKeyParams, out _))
                {
                    Nca0KeyArea = decKeyArea;
                }
                else
                {
                    throw new InvalidDataException("Unable to decrypt NCA0 key area.");
                }
            }
            else
            {
                throw new NotSupportedException();
            }

            return Nca0KeyArea;
        }

        private IStorage OpenNca0BodyStorage(bool openEncrypted)
        {
            // NCA0 encrypts the entire NCA body using AES-XTS instead of
            // using different encryption types and IVs for each section.
            Assert.SdkEqual(0, Header.Version);

            if (openEncrypted == IsEncrypted)
            {
                return GetRawStorage();
            }

            if (Nca0TransformedBody != null)
                return Nca0TransformedBody;

            byte[] key0 = GetContentKey(NcaKeyType.AesXts0);
            byte[] key1 = GetContentKey(NcaKeyType.AesXts1);

            Nca0TransformedBody = new CachedStorage(new Aes128XtsStorage(GetRawStorage(), key0, key1, NcaHeader.HeaderSectorSize, true, !openEncrypted), 1, true);
            return Nca0TransformedBody;

            IStorage GetRawStorage()
            {
                BaseStorage.GetSize(out long ncaSize).ThrowIfFailure();
                return BaseStorage.Slice(0x400, ncaSize - 0x400);
            }
        }

        private IStorage OpenNca0RawStorage(int index, bool openEncrypted)
        {
            if (!SectionExists(index)) throw new ArgumentException(nameof(index), Messages.NcaSectionMissing);

            long offset = Header.GetSectionStartOffset(index) - 0x400;
            long size = Header.GetSectionSize(index);

            IStorage bodyStorage = OpenNca0BodyStorage(openEncrypted);

            bodyStorage.GetSize(out long baseSize).ThrowIfFailure();

            if (!IsSubRange(offset, size, baseSize))
            {
                throw new InvalidDataException(
                    $"Section offset (0x{offset + 0x400:x}) and length (0x{size:x}) fall outside the total NCA length (0x{baseSize + 0x400:x}).");
            }

            return new SubStorage(bodyStorage, offset, size);
        }

        public NcaFsHeader GetNca0FsHeader(int index)
        {
            // NCA0 stores the FS header in the first block of the section instead of the header
            IStorage bodyStorage = OpenNca0BodyStorage(false);
            long offset = Header.GetSectionStartOffset(index) - 0x400;

            byte[] fsHeaderData = new byte[0x200];
            bodyStorage.Read(offset, fsHeaderData).ThrowIfFailure();

            return new NcaFsHeader(fsHeaderData);
        }

        public Validity VerifyHeaderSignature()
        {
            return Header.VerifySignature1(KeySet.NcaHeaderSigningKeyParams[0].Modulus);
        }

        internal void GenerateAesCounter(int sectionIndex, Ncm.ContentType type, int minorVersion)
        {
            int counterType;
            int counterVersion;

            NcaFsHeader header = GetFsHeader(sectionIndex);
            if (header.EncryptionType != NcaEncryptionType.AesCtr &&
                header.EncryptionType != NcaEncryptionType.AesCtrEx) return;

            switch (type)
            {
                case Ncm.ContentType.Program:
                    counterType = sectionIndex + 1;
                    break;
                case Ncm.ContentType.HtmlDocument:
                    counterType = (int)Ncm.ContentType.HtmlDocument;
                    break;
                case Ncm.ContentType.LegalInformation:
                    counterType = (int)Ncm.ContentType.LegalInformation;
                    break;
                default:
                    counterType = 0;
                    break;
            }

            // Version of firmware NCAs appears to always be 0
            // Haven't checked delta fragment NCAs
            switch (Header.ContentType)
            {
                case NcaContentType.Program:
                case NcaContentType.Manual:
                    counterVersion = Math.Max(minorVersion - 1, 0);
                    break;
                case NcaContentType.PublicData:
                    counterVersion = minorVersion << 16;
                    break;
                default:
                    counterVersion = 0;
                    break;
            }

            header.CounterType = counterType;
            header.CounterVersion = counterVersion;
        }

        private static bool IsSubRange(long startIndex, long subLength, long length)
        {
            bool isOutOfRange = startIndex < 0 || startIndex > length || subLength < 0 || startIndex > length - subLength;
            return !isOutOfRange;
        }
    }
}
