using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Util;
using static LibHac.FsSystem.Anonymous;

namespace LibHac.FsSystem;

file static class Anonymous
{
    public static bool IsZero<T>(ReadOnlySpan<T> value) where T : unmanaged
    {
        ReadOnlySpan<byte> valueBytes = MemoryMarshal.Cast<T, byte>(value);
        Span<byte> zero = stackalloc byte[valueBytes.Length];
        zero.Clear();

        return CryptoUtil.IsSameBytes(valueBytes, zero, valueBytes.Length);
    }

    public static bool IsZero<T>(in T value) where T : unmanaged
    {
        ReadOnlySpan<byte> valueBytes = SpanHelpers.AsReadOnlyByteSpan(in value);
        Span<byte> zero = stackalloc byte[Unsafe.SizeOf<T>()];
        zero.Clear();

        return CryptoUtil.IsSameBytes(valueBytes, zero, Unsafe.SizeOf<T>());
    }

    public static bool IsZero<T>(in T value, int startOffset) where T : unmanaged
    {
        ReadOnlySpan<byte> valueBytes = SpanHelpers.AsReadOnlyByteSpan(in value).Slice(startOffset);
        Span<byte> zero = stackalloc byte[Unsafe.SizeOf<T>() - startOffset];
        zero.Clear();

        return CryptoUtil.IsSameBytes(valueBytes, zero, Unsafe.SizeOf<T>() - startOffset);
    }

    public static bool IsIncluded<T>(T value, T min, T max) where T : IComparisonOperators<T, T, bool>
    {
        return min <= value && value <= max;
    }
}

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

    public static ulong SectorToByte(uint sectorIndex) => (ulong)sectorIndex << SectorShift;
    public static uint ByteToSector(ulong byteIndex) => (uint)(byteIndex >> SectorShift);

    public readonly byte GetProperKeyGeneration() => Math.Max(KeyGeneration1, KeyGeneration2);

    public readonly Result Verify()
    {
        const uint magicBodyMask = 0xFFFFFF;
        const uint magicVersionMask = 0xFF000000;
        const uint magicBodyValue = 0x41434E; // NCA
        const uint magicVersionMax = 0x33000000; // \0\0\03

        if ((Magic & magicBodyMask) != magicBodyValue || (Magic & magicVersionMask) > magicVersionMax)
            return ResultFs.InvalidNcaHeader.Log();

        if (!IsIncluded((int)DistributionTypeValue, (int)DistributionType.Download, (int)DistributionType.GameCard))
            return ResultFs.InvalidNcaHeader.Log();

        if (!IsIncluded((int)ContentTypeValue, (int)ContentType.Program, (int)ContentType.PublicData))
            return ResultFs.InvalidNcaHeader.Log();

        if (KeyAreaEncryptionKeyIndex >= NcaCryptoConfiguration.KeyAreaEncryptionKeyIndexCount &&
            KeyAreaEncryptionKeyIndex != NcaCryptoConfiguration.KeyAreaEncryptionKeyIndexZeroKey)
        {
            return ResultFs.InvalidNcaHeader.Log();
        }

        if (ProgramId == 0)
            return ResultFs.InvalidNcaHeader.Log();

        if (SdkAddonVersion == 0)
            return ResultFs.InvalidNcaHeader.Log();

        if (!IsZero(in Reserved222))
            return ResultFs.InvalidNcaHeader.Log();

        if (!IsZero(in Reserved224))
            return ResultFs.InvalidNcaHeader.Log();

        long es = long.MaxValue;

        for (int i = 0; i < FsCountMax; i++)
        {
            if (FsInfos[i].StartSector != 0 || FsInfos[i].EndSector != 0)
            {
                if (es == long.MaxValue)
                    es = FsInfos[i].EndSector;

                if (es < FsInfos[i].EndSector || FsInfos[i].StartSector >= FsInfos[i].EndSector)
                    return ResultFs.InvalidNcaHeader.Log();

                es = FsInfos[i].StartSector;

                if (FsInfos[i].HashSectors != ByteToSector(0x200))
                    return ResultFs.InvalidNcaHeader.Log();
            }
            else if (FsInfos[i].HashSectors != 0)
            {
                return ResultFs.InvalidNcaHeader.Log();
            }

            if (FsInfos[i].Reserved != 0)
                return ResultFs.InvalidNcaHeader.Log();
        }

        const int offset = (int)DecryptionKey.Count * Aes.KeySize128;
        if (!IsZero(EncryptedKeys[..][offset..]))
            return ResultFs.InvalidNcaHeader.Log();

        if (!IsZero(EncryptedKeys[offset..]))
            return ResultFs.InvalidNcaHeader.Log();

        return Result.Success;
    }
}

public struct NcaPatchInfo
{
    public long IndirectOffset;
    public long IndirectSize;
    public Array16<byte> IndirectHeader;
    public long AesCtrExOffset;
    public long AesCtrExSize;
    public Array16<byte> AesCtrExHeader;

    public readonly bool HasIndirectTable()
    {
        return Unsafe.As<Array16<byte>, uint>(ref Unsafe.AsRef(in IndirectHeader)) == BucketTree.Signature;
    }

    public readonly bool HasAesCtrExTable()
    {
        return Unsafe.As<Array16<byte>, uint>(ref Unsafe.AsRef(in AesCtrExHeader)) == BucketTree.Signature;
    }
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

    public readonly bool HasSparseTable()
    {
        return Unsafe.As<Array16<byte>, uint>(ref Unsafe.AsRef(in MetaHeader)) == BucketTree.Signature;
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
        HierarchicalIntegrity = 1,
        HierarchicalIntegritySha3 = 2
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

            public readonly Result Verify()
            {
                if (IsZero(in MasterHash))
                    return ResultFs.InvalidNcaFsHeader.Log();

                if (BlockSize <= 0 || !BitUtil.IsPowerOfTwo(BlockSize))
                    return ResultFs.InvalidNcaFsHeader.Log();

                if (LayerCount != 2)
                    return ResultFs.InvalidNcaFsHeader.Log();

                long currentOffset = 0;

                for (int i = 0; i < LayerCount; i++)
                {
                    if (currentOffset > LayerRegions[i].Offset)
                        return ResultFs.InvalidNcaFsHeader.Log();

                    if (LayerRegions[i].Size <= 0)
                        return ResultFs.InvalidNcaFsHeader.Log();

                    currentOffset = LayerRegions[i].Offset + LayerRegions[i].Size;
                }

                for (int i = LayerCount; i < 5; i++)
                {
                    if (!IsZero(in LayerRegions[i]))
                        return ResultFs.InvalidNcaFsHeader.Log();
                }

                return Result.Success;
            }
        }

        public struct IntegrityMetaInfo
        {
            public const int HashSize = Sha256Generator.HashSize;

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

            public readonly Result Verify()
            {
                if (Magic != HierarchicalIntegrityVerificationStorage.IntegrityVerificationStorageMagic)
                    return ResultFs.InvalidNcaFsHeader.Log();

                if (Version != HierarchicalIntegrityVerificationStorage.IntegrityVerificationStorageVersion)
                    return ResultFs.InvalidNcaFsHeader.Log();

                if (MasterHashSize != HashSize)
                    return ResultFs.InvalidNcaFsHeader.Log();

                if (!IsIncluded(LevelHashInfo.MaxLayers, Constants.IntegrityMinLayerCount, Constants.IntegrityMaxLayerCount))
                    return ResultFs.InvalidNcaFsHeader.Log();

                long currentOffset = 0;

                for (int i = 0; i < LevelHashInfo.MaxLayers - 1; i++)
                {
                    ref readonly InfoLevelHash.HierarchicalIntegrityVerificationLevelInformation layer = ref LevelHashInfo.Layers[i];

                    if (layer.OrderBlock <= 0)
                        return ResultFs.InvalidNcaFsHeader.Log();

                    if (currentOffset > layer.Offset || !Alignment.IsAligned(layer.Offset.Get(), (ulong)(1 << layer.OrderBlock)))
                        return ResultFs.InvalidNcaFsHeader.Log();

                    if (layer.Size <= 0)
                        return ResultFs.InvalidNcaFsHeader.Log();

                    if (!IsZero<byte>(layer.Reserved))
                        return ResultFs.InvalidNcaFsHeader.Log();

                    currentOffset = layer.Offset + layer.Size;
                }

                for (int i = LevelHashInfo.MaxLayers - 1; i < 6; i++)
                {
                    if (!IsZero(in LevelHashInfo.Layers[i]))
                        return ResultFs.InvalidNcaFsHeader.Log();
                }

                return Result.Success;
            }
        }
    }

    public Result Verify()
    {
        if (Version != 2)
            return ResultFs.InvalidNcaFsHeader.Log();

        if (!IsIncluded((int)FsTypeValue, (int)FsType.RomFs, (int)FsType.PartitionFs))
            return ResultFs.InvalidNcaFsHeader.Log();

        if (!IsIncluded((int)HashTypeValue, (int)HashType.None, (int)HashType.HierarchicalIntegritySha3Hash) || HashTypeValue == HashType.AutoSha3)
            return ResultFs.InvalidNcaFsHeader.Log();

        if (!IsIncluded((int)EncryptionTypeValue, (int)EncryptionType.None, (int)EncryptionType.AesCtrExSkipLayerHash))
            return ResultFs.InvalidNcaFsHeader.Log();

        if (!IsIncluded((int)MetaDataHashTypeValue, (int)MetaDataHashType.None, (int)MetaDataHashType.HierarchicalIntegritySha3))
            return ResultFs.InvalidNcaFsHeader.Log();

        if (!IsZero(in Reserved))
            return ResultFs.InvalidNcaFsHeader.Log();

        switch (HashTypeValue)
        {
            case HashType.HierarchicalSha256Hash:
            case HashType.HierarchicalSha3256Hash:
            {
                Result res = HashDataValue.HierarchicalSha256.Verify();
                if (res.IsFailure()) return res.Miss();

                if (!IsZero(in HashDataValue, Unsafe.SizeOf<HashData.HierarchicalSha256Data>()))
                    return ResultFs.InvalidNcaFsHeader.Log();

                break;
            }
            case HashType.HierarchicalIntegrityHash:
            case HashType.HierarchicalIntegritySha3Hash:
            {
                Result res = HashDataValue.IntegrityMeta.Verify();
                if (res.IsFailure()) return res.Miss();

                if (!IsZero(in HashDataValue, Unsafe.SizeOf<HashData.IntegrityMetaInfo>()))
                    return ResultFs.InvalidNcaFsHeader.Log();

                break;
            }
            default:
            {
                if (!IsZero(in HashDataValue))
                    return ResultFs.InvalidNcaFsHeader.Log();

                break;
            }
        }

        if (EncryptionTypeValue == EncryptionType.AesCtrEx || EncryptionTypeValue == EncryptionType.AesCtrExSkipLayerHash)
        {
            if (PatchInfo.IndirectOffset < 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            if (PatchInfo.IndirectSize <= 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            if (IsZero(in PatchInfo.IndirectHeader))
                return ResultFs.InvalidNcaFsHeader.Log();

            if (PatchInfo.AesCtrExOffset < 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            if (MetaDataHashTypeValue == MetaDataHashType.None)
            {
                if (PatchInfo.AesCtrExSize <= 0)
                    return ResultFs.InvalidNcaFsHeader.Log();
            }
            else if (PatchInfo.IndirectOffset == 0)
            {
                if (PatchInfo.AesCtrExSize != 0)
                    return ResultFs.InvalidNcaFsHeader.Log();
            }
            else if (PatchInfo.AesCtrExSize <= 0)
            {
                return ResultFs.InvalidNcaFsHeader.Log();
            }

            if (IsZero(in PatchInfo.AesCtrExHeader))
                return ResultFs.InvalidNcaFsHeader.Log();
        }
        else if (EncryptionTypeValue != EncryptionType.None && !IsZero(in PatchInfo))
        {
            return ResultFs.InvalidNcaFsHeader.Log();
        }

        if (EncryptionTypeValue != EncryptionType.AesCtr
            && EncryptionTypeValue != EncryptionType.AesCtrEx
            && EncryptionTypeValue != EncryptionType.AesCtrSkipLayerHash
            && EncryptionTypeValue != EncryptionType.AesCtrExSkipLayerHash
            && !IsZero(in AesCtrUpperIv))
        {
            return ResultFs.InvalidNcaFsHeader.Log();
        }

        if (SparseInfo.Generation != 0)
        {
            if (SparseInfo.MetaOffset < 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            if (SparseInfo.MetaSize < 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            if (SparseInfo.PhysicalOffset < 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            if (!IsZero(SparseInfo.Reserved))
                return ResultFs.InvalidNcaFsHeader.Log();

            var header = SpanHelpers.AsStruct<BucketTree.Header>(SparseInfo.MetaHeader);
            Result res = header.Verify();
            if (res.IsFailure()) return res.Miss();
        }
        else if (!IsZero(in SparseInfo))
        {
            return ResultFs.InvalidNcaFsHeader.Log();
        }

        if (MetaDataHashTypeValue != MetaDataHashType.None)
        {
            if (MetaDataHashDataInfo.Offset <= 0)
                return ResultFs.InvalidNcaFsHeader.Log();

            if (MetaDataHashDataInfo.Size <= 0)
                return ResultFs.InvalidNcaFsHeader.Log();
        }
        else if (!IsZero(MetaDataHashDataInfo))
        {
            return ResultFs.InvalidNcaFsHeader.Log();
        }

        if (!IsZero(Padding))
            return ResultFs.InvalidNcaFsHeader.Log();

        return Result.Success;
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