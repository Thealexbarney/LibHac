using System.Runtime.CompilerServices;
using LibHac.FsSystem;
using LibHac.FsSystem.Impl;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.FsSystem;

public class TypeLayoutTests
{
    [Fact]
    public static void Hash_Layout()
    {
        Hash s = default;

        Assert.Equal(0x20, Unsafe.SizeOf<Hash>());

        Assert.Equal(0x0, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void NcaFsHeader_Layout()
    {
        NcaFsHeader s = default;

        Assert.Equal(0x200, Unsafe.SizeOf<NcaFsHeader>());

        Assert.Equal(0x000, GetOffset(in s, in s.Version));
        Assert.Equal(0x002, GetOffset(in s, in s.FsTypeValue));
        Assert.Equal(0x003, GetOffset(in s, in s.HashTypeValue));
        Assert.Equal(0x004, GetOffset(in s, in s.EncryptionTypeValue));
        Assert.Equal(0x005, GetOffset(in s, in s.MetaDataHashTypeValue));
        Assert.Equal(0x006, GetOffset(in s, in s.Reserved));
        Assert.Equal(0x008, GetOffset(in s, in s.HashDataValue));
        Assert.Equal(0x100, GetOffset(in s, in s.PatchInfo));
        Assert.Equal(0x140, GetOffset(in s, in s.AesCtrUpperIv));
        Assert.Equal(0x148, GetOffset(in s, in s.SparseInfo));
        Assert.Equal(0x178, GetOffset(in s, in s.CompressionInfo));
        Assert.Equal(0x1A0, GetOffset(in s, in s.MetaDataHashDataInfo));
        Assert.Equal(0x1D0, GetOffset(in s, in s.Padding));
    }

    [Fact]
    public static void NcaFsHeaderRegion_Layout()
    {
        NcaFsHeader.Region s = default;

        Assert.Equal(0x10, Unsafe.SizeOf<NcaFsHeader.Region>());

        Assert.Equal(0, GetOffset(in s, in s.Offset));
        Assert.Equal(8, GetOffset(in s, in s.Size));
    }

    [Fact]
    public static void HashData_Layout()
    {
        NcaFsHeader.HashData s = default;

        Assert.Equal(0xF8, Unsafe.SizeOf<NcaFsHeader.HashData>());

        Assert.Equal(0, GetOffset(in s, in s.HierarchicalSha256));
        Assert.Equal(0, GetOffset(in s, in s.IntegrityMeta));
    }

    [Fact]
    public static void HierarchicalSha256Data_Layout()
    {
        NcaFsHeader.HashData.HierarchicalSha256Data s = default;

        Assert.Equal(0x78, Unsafe.SizeOf<NcaFsHeader.HashData.HierarchicalSha256Data>());

        Assert.Equal(0x00, GetOffset(in s, in s.MasterHash));
        Assert.Equal(0x20, GetOffset(in s, in s.BlockSize));
        Assert.Equal(0x24, GetOffset(in s, in s.LayerCount));
        Assert.Equal(0x28, GetOffset(in s, in s.LayerRegions));
    }

    [Fact]
    public static void IntegrityMetaInfo_Layout()
    {
        NcaFsHeader.HashData.IntegrityMetaInfo s = default;

        Assert.Equal(0xE0, Unsafe.SizeOf<NcaFsHeader.HashData.IntegrityMetaInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.Version));
        Assert.Equal(0x08, GetOffset(in s, in s.MasterHashSize));
        Assert.Equal(0x0C, GetOffset(in s, in s.LevelHashInfo));
        Assert.Equal(0xC0, GetOffset(in s, in s.MasterHash));
    }

    [Fact]
    public static void InfoLevelHash_Layout()
    {
        NcaFsHeader.HashData.IntegrityMetaInfo.InfoLevelHash s = default;

        Assert.Equal(0xB4, Unsafe.SizeOf<NcaFsHeader.HashData.IntegrityMetaInfo.InfoLevelHash>());

        Assert.Equal(0x00, GetOffset(in s, in s.MaxLayers));
        Assert.Equal(0x04, GetOffset(in s, in s.Layers));
        Assert.Equal(0x94, GetOffset(in s, in s.Salt));
    }

    [Fact]
    public static void InfoLevelHash_HierarchicalIntegrityVerificationLevelInformation_Layout()
    {
        NcaFsHeader.HashData.IntegrityMetaInfo.InfoLevelHash.HierarchicalIntegrityVerificationLevelInformation s = default;

        Assert.Equal(0x18, Unsafe.SizeOf<NcaFsHeader.HashData.IntegrityMetaInfo.InfoLevelHash.HierarchicalIntegrityVerificationLevelInformation>());

        Assert.Equal(0x00, GetOffset(in s, in s.Offset));
        Assert.Equal(0x08, GetOffset(in s, in s.Size));
        Assert.Equal(0x10, GetOffset(in s, in s.OrderBlock));
        Assert.Equal(0x14, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void InfoLevelHash_SignatureSalt_Layout()
    {
        NcaFsHeader.HashData.IntegrityMetaInfo.InfoLevelHash.SignatureSalt s = default;

        Assert.Equal(0x20, Unsafe.SizeOf<NcaFsHeader.HashData.IntegrityMetaInfo.InfoLevelHash.SignatureSalt>());

        Assert.Equal(0, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void NcaPatchInfo_Layout()
    {
        NcaPatchInfo s = default;

        Assert.Equal(0x40, Unsafe.SizeOf<NcaPatchInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.IndirectOffset));
        Assert.Equal(0x08, GetOffset(in s, in s.IndirectSize));
        Assert.Equal(0x10, GetOffset(in s, in s.IndirectHeader));
        Assert.Equal(0x20, GetOffset(in s, in s.AesCtrExOffset));
        Assert.Equal(0x28, GetOffset(in s, in s.AesCtrExSize));
        Assert.Equal(0x30, GetOffset(in s, in s.AesCtrExHeader));
    }

    [Fact]
    public static void NcaSparseInfo_Layout()
    {
        NcaSparseInfo s = default;

        Assert.Equal(0x30, Unsafe.SizeOf<NcaSparseInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.MetaOffset));
        Assert.Equal(0x08, GetOffset(in s, in s.MetaSize));
        Assert.Equal(0x10, GetOffset(in s, in s.MetaHeader));
        Assert.Equal(0x20, GetOffset(in s, in s.PhysicalOffset));
        Assert.Equal(0x28, GetOffset(in s, in s.Generation));
        Assert.Equal(0x2A, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void NcaCompressionInfo_Layout()
    {
        NcaCompressionInfo s = default;

        Assert.Equal(0x28, Unsafe.SizeOf<NcaCompressionInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.TableOffset));
        Assert.Equal(0x08, GetOffset(in s, in s.TableSize));
        Assert.Equal(0x10, GetOffset(in s, in s.TableHeader));
        Assert.Equal(0x20, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void NcaMetaDataHashDataInfo_Layout()
    {
        NcaMetaDataHashDataInfo s = default;

        Assert.Equal(0x30, Unsafe.SizeOf<NcaMetaDataHashDataInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.Offset));
        Assert.Equal(0x08, GetOffset(in s, in s.Size));
        Assert.Equal(0x10, GetOffset(in s, in s.Hash));
    }

    [Fact]
    public static void NcaMetaDataHashData_Layout()
    {
        NcaMetaDataHashData s = default;

        Assert.Equal(0xE8, Unsafe.SizeOf<NcaMetaDataHashData>());

        Assert.Equal(0, GetOffset(in s, in s.LayerInfoOffset));
        Assert.Equal(8, GetOffset(in s, in s.IntegrityMetaInfo));
    }

    [Fact]
    public static void NcaAesCtrUpperIv_Layout()
    {
        NcaAesCtrUpperIv s = default;

        Assert.Equal(8, Unsafe.SizeOf<NcaAesCtrUpperIv>());

        Assert.Equal(0, GetOffset(in s, in s.Value));
        Assert.Equal(0, GetOffset(in s, in s.Generation));
        Assert.Equal(4, GetOffset(in s, in s.SecureValue));
    }

    [Fact]
    public static void NcaHeader_Layout()
    {
        NcaHeader s = default;

        Assert.Equal(0x400, Unsafe.SizeOf<NcaHeader>());

        Assert.Equal(0x000, GetOffset(in s, in s.Signature1));
        Assert.Equal(0x100, GetOffset(in s, in s.Signature2));
        Assert.Equal(0x200, GetOffset(in s, in s.Magic));
        Assert.Equal(0x204, GetOffset(in s, in s.DistributionTypeValue));
        Assert.Equal(0x205, GetOffset(in s, in s.ContentTypeValue));
        Assert.Equal(0x206, GetOffset(in s, in s.KeyGeneration1));
        Assert.Equal(0x207, GetOffset(in s, in s.KeyAreaEncryptionKeyIndex));
        Assert.Equal(0x208, GetOffset(in s, in s.ContentSize));
        Assert.Equal(0x210, GetOffset(in s, in s.ProgramId));
        Assert.Equal(0x218, GetOffset(in s, in s.ContentIndex));
        Assert.Equal(0x21C, GetOffset(in s, in s.SdkAddonVersion));
        Assert.Equal(0x220, GetOffset(in s, in s.KeyGeneration2));
        Assert.Equal(0x221, GetOffset(in s, in s.Header1SignatureKeyGeneration));
        Assert.Equal(0x222, GetOffset(in s, in s.Reserved222));
        Assert.Equal(0x224, GetOffset(in s, in s.Reserved224));
        Assert.Equal(0x230, GetOffset(in s, in s.RightsId));
        Assert.Equal(0x240, GetOffset(in s, in s.FsInfos));
        Assert.Equal(0x280, GetOffset(in s, in s.FsHeaderHashes));
        Assert.Equal(0x300, GetOffset(in s, in s.EncryptedKeys));

        Assert.Equal(NcaHeader.Size, Unsafe.SizeOf<NcaHeader>());
        Assert.Equal(NcaHeader.SectorSize, 1 << NcaHeader.SectorShift);

        Assert.Equal(NcaHeader.HeaderSignSize, s.Signature1.ItemsRo.Length);
        Assert.Equal(NcaHeader.HeaderSignSize, s.Signature2.ItemsRo.Length);
        Assert.Equal(NcaHeader.RightsIdSize, s.RightsId.ItemsRo.Length);
        Assert.Equal(NcaHeader.FsCountMax, s.FsInfos.ItemsRo.Length);
        Assert.Equal(NcaHeader.FsCountMax, s.FsHeaderHashes.ItemsRo.Length);
        Assert.Equal(NcaHeader.EncryptedKeyAreaSize, s.EncryptedKeys.ItemsRo.Length);
    }

    [Fact]
    public static void NcaHeader_FsInfo_Layout()
    {
        NcaHeader.FsInfo s = default;

        Assert.Equal(0x10, Unsafe.SizeOf<NcaHeader.FsInfo>());

        Assert.Equal(0x0, GetOffset(in s, in s.StartSector));
        Assert.Equal(0x4, GetOffset(in s, in s.EndSector));
        Assert.Equal(0x8, GetOffset(in s, in s.HashSectors));
        Assert.Equal(0xC, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void KeyType_Layout()
    {
        NcaCryptoConfiguration s = default;

        Assert.Equal(NcaCryptoConfiguration.Header1SignatureKeyGenerationMax + 1, s.Header1SignKeyModuli.ItemsRo.Length);
        Assert.Equal(NcaCryptoConfiguration.Rsa2048KeyModulusSize, s.Header1SignKeyModuli.ItemsRo[0].ItemsRo.Length);
        Assert.Equal(NcaCryptoConfiguration.Rsa2048KeyPublicExponentSize, s.Header1SignKeyPublicExponent.ItemsRo.Length);
        Assert.Equal(NcaCryptoConfiguration.KeyAreaEncryptionKeyIndexCount, s.KeyAreaEncryptionKeySources.ItemsRo.Length);
        Assert.Equal(NcaCryptoConfiguration.Aes128KeySize, s.KeyAreaEncryptionKeySources.ItemsRo[0].ItemsRo.Length);
        Assert.Equal(NcaCryptoConfiguration.Aes128KeySize, s.HeaderEncryptionKeySource.ItemsRo.Length);
        Assert.Equal(NcaCryptoConfiguration.HeaderEncryptionKeyCount, s.HeaderEncryptedEncryptionKeys.ItemsRo.Length);
        Assert.Equal(NcaCryptoConfiguration.Aes128KeySize, s.HeaderEncryptedEncryptionKeys.ItemsRo[0].ItemsRo.Length);
    }

    [Fact]
    public static void AesCtrCounterExtendedStorage_Entry_Layout()
    {
        AesCtrCounterExtendedStorage.Entry s = default;

        Assert.Equal(0x10, Unsafe.SizeOf<AesCtrCounterExtendedStorage.Entry>());

        Assert.Equal(0x0, GetOffset(in s, in s.Offset));
        Assert.Equal(0x8, GetOffset(in s, in s.EncryptionValue));
        Assert.Equal(0x9, GetOffset(in s, in s.Reserved));
        Assert.Equal(0xC, GetOffset(in s, in s.Generation));
    }

    [Fact]
    public static void HierarchicalIntegrityVerificationLevelInformation_Layout()
    {
        HierarchicalIntegrityVerificationLevelInformation s = default;

        Assert.Equal(0x18, Unsafe.SizeOf<HierarchicalIntegrityVerificationLevelInformation>());
        Assert.Equal(0x04, AlignOf<HierarchicalIntegrityVerificationLevelInformation>());

        Assert.Equal(0x00, GetOffset(in s, in s.Offset));
        Assert.Equal(0x08, GetOffset(in s, in s.Size));
        Assert.Equal(0x10, GetOffset(in s, in s.BlockOrder));
        Assert.Equal(0x14, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void HierarchicalIntegrityVerificationInformation_Layout()
    {
        HierarchicalIntegrityVerificationInformation s = default;

        Assert.Equal(0xB4, Unsafe.SizeOf<HierarchicalIntegrityVerificationInformation>());

        Assert.Equal(0x00, GetOffset(in s, in s.MaxLayers));
        Assert.Equal(0x04, GetOffset(in s, in s.Layers));
        Assert.Equal(0x94, GetOffset(in s, in s.HashSalt));

        Assert.Equal(Constants.IntegrityMaxLayerCount - 1, s.Layers.ItemsRo.Length);
    }

    [Fact]
    public static void HierarchicalIntegrityVerificationMetaInformation_Layout()
    {
        HierarchicalIntegrityVerificationMetaInformation s = default;

        Assert.Equal(0xC0, Unsafe.SizeOf<HierarchicalIntegrityVerificationMetaInformation>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.Version));
        Assert.Equal(0x08, GetOffset(in s, in s.MasterHashSize));
        Assert.Equal(0x0C, GetOffset(in s, in s.LevelHashInfo));
    }

    [Fact]
    public static void HierarchicalIntegrityVerificationSizeSet_Layout()
    {
        HierarchicalIntegrityVerificationSizeSet s = default;

        Assert.Equal(Constants.IntegrityMaxLayerCount - 2, s.LayeredHashSizes.ItemsRo.Length);
    }

    [Fact]
    public static void HierarchicalIntegrityVerificationStorageControlArea_InputParam_Layout()
    {
        HierarchicalIntegrityVerificationStorageControlArea.InputParam s = default;

        Assert.Equal(Constants.IntegrityMaxLayerCount - 1, s.LevelBlockSizes.ItemsRo.Length);
    }

    [Fact]
    public static void PartitionFileSystemFormat_PartitionEntry_Layout()
    {
        PartitionFileSystemFormat.PartitionEntry s = default;

        Assert.Equal(0x18, Unsafe.SizeOf<PartitionFileSystemFormat.PartitionEntry>());

        Assert.Equal(0x00, GetOffset(in s, in s.Offset));
        Assert.Equal(0x08, GetOffset(in s, in s.Size));
        Assert.Equal(0x10, GetOffset(in s, in s.NameOffset));
        Assert.Equal(0x14, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void PartitionFileSystemFormat_PartitionFileSystemHeaderImpl_Layout()
    {
        PartitionFileSystemFormat.PartitionFileSystemHeaderImpl s = default;

        Assert.Equal(0x10, Unsafe.SizeOf<PartitionFileSystemFormat.PartitionFileSystemHeaderImpl>());

        Assert.Equal(0x0, GetOffset(in s, in s.Signature[0]));
        Assert.Equal(0x4, GetOffset(in s, in s.EntryCount));
        Assert.Equal(0x8, GetOffset(in s, in s.NameTableSize));
        Assert.Equal(0xC, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void Sha256PartitionFileSystemFormat_PartitionEntry_Layout()
    {
        Sha256PartitionFileSystemFormat.PartitionEntry s = default;

        Assert.Equal(0x40, Unsafe.SizeOf<Sha256PartitionFileSystemFormat.PartitionEntry>());

        Assert.Equal(0x00, GetOffset(in s, in s.Offset));
        Assert.Equal(0x08, GetOffset(in s, in s.Size));
        Assert.Equal(0x10, GetOffset(in s, in s.NameOffset));
        Assert.Equal(0x14, GetOffset(in s, in s.HashTargetSize));
        Assert.Equal(0x18, GetOffset(in s, in s.HashTargetOffset));
        Assert.Equal(0x20, GetOffset(in s, in s.Hash));
    }
}