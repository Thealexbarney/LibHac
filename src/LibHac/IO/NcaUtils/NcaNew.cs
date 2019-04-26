using System;
using System.Diagnostics;
using System.IO;

namespace LibHac.IO.NcaUtils
{
    public class NcaNew
    {
        private Keyset Keyset { get; }
        private IStorage BaseStorage { get; }

        public NcaHeaderNew Header { get; }
        public byte[] TitleKey { get; }

        public NcaNew(Keyset keyset, IStorage storage)
        {
            Keyset = keyset;
            BaseStorage = storage;
            Header = new NcaHeaderNew(keyset, storage);

            keyset.TitleKeys.TryGetValue(Header.RightsId.ToArray(), out byte[] titleKey);
            TitleKey = titleKey;
        }

        public byte[] GetDecryptedKey(int index)
        {
            if (index < 0 || index > 3) throw new ArgumentOutOfRangeException(nameof(index));

            int generation = Util.GetMasterKeyRevision(Header.KeyGeneration);
            byte[] keyAreaKey = Keyset.KeyAreaKeys[generation][Header.KeyAreaKeyIndex];

            if (keyAreaKey.IsEmpty())
            {
                string keyName = $"key_area_key_{Keyset.KakNames[Header.KeyAreaKeyIndex]}_{generation:x2}";
                throw new MissingKeyException("Unable to decrypt NCA section.", keyName, KeyType.Common);
            }

            byte[] encryptedKey = Header.GetEncryptedKey(index).ToArray();
            var decryptedKey = new byte[Crypto.Aes128Size];

            Crypto.DecryptEcb(keyAreaKey, encryptedKey, decryptedKey, Crypto.Aes128Size);

            return decryptedKey;
        }

        public byte[] GetDecryptedTitleKey()
        {
            int generation = Util.GetMasterKeyRevision(Header.KeyGeneration);
            byte[] titleKek = Keyset.TitleKeks[generation];

            if (!Keyset.TitleKeys.TryGetValue(Header.RightsId.ToArray(), out byte[] encryptedKey))
            {
                throw new MissingKeyException("Missing NCA title key.", Header.RightsId.ToHexString(), KeyType.Title);
            }

            if (titleKek.IsEmpty())
            {
                string keyName = $"titlekek_{generation:x2}";
                throw new MissingKeyException("Unable to decrypt title key.", keyName, KeyType.Common);
            }

            var decryptedKey = new byte[Crypto.Aes128Size];

            Crypto.DecryptEcb(titleKek, encryptedKey, decryptedKey, Crypto.Aes128Size);

            return decryptedKey;
        }

        internal byte[] GetContentKey(NcaKeyType type)
        {
            return Header.RightsId.IsEmpty ? GetDecryptedKey((int)type) : GetDecryptedTitleKey();
        }

        private IStorage OpenEncryptedStorage(int index)
        {
            if (!Header.IsSectionEnabled(index)) throw new ArgumentOutOfRangeException(nameof(index), "Section is empty");

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
            NcaFsHeaderNew header = Header.GetFsHeader(index);

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

        private IStorage OpenAesXtsStorage(IStorage baseStorage, int index)
        {
            throw new NotImplementedException("NCA sections using XTS are not supported yet.");
        }

        private IStorage OpenAesCtrStorage(IStorage baseStorage, int index)
        {
            NcaFsHeaderNew fsHeader = Header.GetFsHeader(index);
            byte[] key = GetContentKey(NcaKeyType.AesCtr);
            byte[] counter = Aes128CtrStorage.CreateCounter(fsHeader.Counter, Header.GetSectionStartOffset(index));

            var aesStorage = new Aes128CtrStorage(baseStorage, key, Header.GetSectionStartOffset(index), counter, true);
            return new CachedStorage(aesStorage, 0x4000, 4, true);
        }

        private IStorage OpenAesCtrExStorage(IStorage baseStorage, int index)
        {
            NcaFsHeaderNew fsHeader = Header.GetFsHeader(index);
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

        public IStorage OpenStorage(int index, IntegrityCheckLevel integrityCheckLevel)
        {
            IStorage rawStorage = OpenRawStorage(index);

            NcaFsHeaderNew header = Header.GetFsHeader(index);
            header.GetIntegrityInfoSha256();

            // todo don't assume that ctr ex means it's a patch
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
    }
}
