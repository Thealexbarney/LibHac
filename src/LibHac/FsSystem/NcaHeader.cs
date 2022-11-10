using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// The structure used as the header for an NCA file.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public struct NcaHeader
{
    public enum ContentType : byte
    {
        Program = 0,
        Meta = 1,
        Control = 2,
        Manual = 3,
        Data = 4,
        PublicData = 5
    }

    public enum DistributionType : byte
    {
        Download = 0,
        GameCard = 1
    }

    public enum EncryptionType : byte
    {
        Auto = 0,
        None = 1
    }

    public enum DecryptionKey : byte
    {
        AesXts = 0,
        AesXts1 = 0,
        AesXts2 = 1,
        AesCtr = 2,
        AesCtrEx = 3,
        AesCtrHw = 4,
        Count
    }

    public struct FsInfo
    {
        public uint StartSector;
        public uint EndSector;
        public uint HashSectors;
        public uint Reserved;
    }

    public static readonly uint Magic0 = 0x3041434E; // NCA0
    public static readonly uint Magic1 = 0x3141434E; // NCA1
    public static readonly uint Magic2 = 0x3241434E; // NCA2
    public static readonly uint Magic3 = 0x3341434E; // NCA3

    public static readonly uint CurrentMagic = Magic3;

    public static readonly int Size = 0x400;
    public static readonly int FsCountMax = 4;
    public static readonly int HeaderSignCount = 2;
    public static readonly int HeaderSignSize = 0x100;
    public static readonly int EncryptedKeyAreaSize = 0x100;
    public static readonly int SectorSize = 0x200;
    public static readonly int SectorShift = 9;
    public static readonly int RightsIdSize = 0x10;
    public static readonly int XtsBlockSize = 0x200;
    public static readonly int CtrBlockSize = 0x10;

    public Array256<byte> Signature1;
    public Array256<byte> Signature2;
    public uint Magic;
    public DistributionType DistributionTypeValue;
    public ContentType ContentTypeValue;
    public byte KeyGeneration1;
    public byte KeyAreaEncryptionKeyIndex;
    public ulong ContentSize;
    public ulong ProgramId;
    public uint ContentIndex;
    public uint SdkAddonVersion;
    public byte KeyGeneration2;
    public byte Header1SignatureKeyGeneration;
    public Array2<byte> Reserved222;
    public Array3<uint> Reserved224;
    public Array16<byte> RightsId;
    public Array4<FsInfo> FsInfos;
    public Array4<Hash> FsHeaderHashes;
    public Array256<byte> EncryptedKeys;

    public static ulong SectorToByte(uint sectorIndex) => sectorIndex << SectorShift;
    public static uint ByteToSector(ulong byteIndex) => (uint)(byteIndex >> SectorShift);

    public readonly byte GetProperKeyGeneration() => Math.Max(KeyGeneration1, KeyGeneration2);
}

public struct NcaPatchInfo
{
    public long IndirectOffset;
    public long IndirectSize;
    public Array16<byte> IndirectHeader;
    public long AesCtrExOffset;
    public long AesCtrExSize;
    public Array16<byte> AesCtrExHeader;

    public readonly bool HasIndirectTable() => IndirectSize != 0;
    public readonly bool HasAesCtrExTable() => AesCtrExSize != 0;
}

public struct NcaSparseInfo
{
    public long MetaOffset;
    public long MetaSize;
    public Array16<byte> MetaHeader;
    public long PhysicalOffset;
    public ushort Generation;
    public Array6<byte> Reserved;

    public readonly uint GetGeneration() => (uint)(Generation << 16);
    public readonly long GetPhysicalSize() => MetaOffset + MetaSize;

    public readonly NcaAesCtrUpperIv MakeAesCtrUpperIv(NcaAesCtrUpperIv upperIv)
    {
        NcaAesCtrUpperIv sparseUpperIv = upperIv;
        sparseUpperIv.Generation = GetGeneration();
        return sparseUpperIv;
    }
}

public struct NcaCompressionInfo
{
    public long TableOffset;
    public long TableSize;
    public Array16<byte> TableHeader;
    public ulong Reserved;
}

public struct NcaMetaDataHashDataInfo
{
    public long Offset;
    public long Size;
    public Hash Hash;
}

public struct NcaMetaDataHashData
{
    public long LayerInfoOffset;
    public NcaFsHeader.HashData.IntegrityMetaInfo IntegrityMetaInfo;
}

[StructLayout(LayoutKind.Explicit)]
public struct NcaAesCtrUpperIv
{
    [FieldOffset(0)] public ulong Value;

    [FieldOffset(0)] public uint Generation;
    [FieldOffset(4)] public uint SecureValue;

    internal NcaAesCtrUpperIv(ulong value)
    {
        Unsafe.SkipInit(out Generation);
        Unsafe.SkipInit(out SecureValue);
        Value = value;
    }
}

/// <summary>
/// The structure used as the header for an NCA file system.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public struct NcaFsHeader
{
    public ushort Version;
    public FsType FsTypeValue;
    public HashType HashTypeValue;
    public EncryptionType EncryptionTypeValue;
    public MetaDataHashType MetaDataHashTypeValue;
    public Array2<byte> Reserved;
    public HashData HashDataValue;
    public NcaPatchInfo PatchInfo;
    public NcaAesCtrUpperIv AesCtrUpperIv;
    public NcaSparseInfo SparseInfo;
    public NcaCompressionInfo CompressionInfo;
    public NcaMetaDataHashDataInfo MetaDataHashDataInfo;
    public Array48<byte> Padding;

    public enum FsType : byte
    {
        RomFs = 0,
        PartitionFs = 1
    }

    public enum EncryptionType : byte
    {
        Auto = 0,
        None = 1,
        AesXts = 2,
        AesCtr = 3,
        AesCtrEx = 4,
        AesCtrSkipLayerHash = 5,
        AesCtrExSkipLayerHash = 6
    }

    public enum HashType : byte
    {
        Auto = 0,
        None = 1,
        HierarchicalSha256Hash = 2,
        HierarchicalIntegrityHash = 3,
        AutoSha3 = 4,
        HierarchicalSha3256Hash = 5,
        HierarchicalIntegritySha3Hash = 6
    }

    public enum MetaDataHashType : byte
    {
        None = 0,
        HierarchicalIntegrity = 1
    }

    public struct Region
    {
        public long Offset;
        public long Size;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0xF8)]
    public struct HashData
    {
        [FieldOffset(0)] public HierarchicalSha256Data HierarchicalSha256;
        [FieldOffset(0)] public IntegrityMetaInfo IntegrityMeta;

        public struct HierarchicalSha256Data
        {
            public Hash MasterHash;
            public int BlockSize;
            public int LayerCount;
            public Array5<Region> LayerRegions;
        }

        public struct IntegrityMetaInfo
        {
            public uint Magic;
            public uint Version;
            public uint MasterHashSize;
            public InfoLevelHash LevelHashInfo;
            public Hash MasterHash;

            public struct InfoLevelHash
            {
                public int MaxLayers;
                public Array6<HierarchicalIntegrityVerificationLevelInformation> Layers;
                public SignatureSalt Salt;

                public struct HierarchicalIntegrityVerificationLevelInformation
                {
                    public Fs.Int64 Offset;
                    public Fs.Int64 Size;
                    public int OrderBlock;
                    public Array4<byte> Reserved;
                }

                public struct SignatureSalt
                {
                    public Array32<byte> Value;
                }
            }
        }
    }

    public readonly Result GetHashTargetOffset(out long outOffset)
    {
        UnsafeHelpers.SkipParamInit(out outOffset);

        if (HashTypeValue is HashType.HierarchicalIntegrityHash or HashType.HierarchicalIntegritySha3Hash)
        {
            ref readonly HashData.IntegrityMetaInfo.InfoLevelHash hashInfo = ref HashDataValue.IntegrityMeta.LevelHashInfo;
            outOffset = hashInfo.Layers[hashInfo.MaxLayers - 2].Offset;
        }
        else if (HashTypeValue is HashType.HierarchicalSha256Hash or HashType.HierarchicalSha3256Hash)
        {
            ref readonly HashData.HierarchicalSha256Data hashInfo = ref HashDataValue.HierarchicalSha256;
            outOffset = hashInfo.LayerRegions[hashInfo.LayerCount - 1].Offset;
        }
        else
        {
            return ResultFs.InvalidNcaFsHeader.Log();
        }

        return Result.Success;
    }

    public readonly bool IsSkipLayerHashEncryption()
    {
        return EncryptionTypeValue is EncryptionType.AesCtrSkipLayerHash or EncryptionType.AesCtrExSkipLayerHash;
    }
}