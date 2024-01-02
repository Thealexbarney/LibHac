using System.Runtime.CompilerServices;
using LibHac.FsSrv;
using LibHac.FsSrv.Impl;
using LibHac.FsSrv.Sf;
using LibHac.FsSrv.Storage;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.FsSrv;

public class TypeLayoutTests
{
    [Fact]
    public static void AccessControlDescriptor_Layout()
    {
        var s = new AccessControlDescriptor();

        Assert.Equal(0x2C, Unsafe.SizeOf<AccessControlDescriptor>());

        Assert.Equal(0x00, GetOffset(in s, in s.Version));
        Assert.Equal(0x01, GetOffset(in s, in s.ContentOwnerIdCount));
        Assert.Equal(0x02, GetOffset(in s, in s.SaveDataOwnerIdCount));
        Assert.Equal(0x04, GetOffset(in s, in s.AccessFlags));
        Assert.Equal(0x0C, GetOffset(in s, in s.ContentOwnerIdMin));
        Assert.Equal(0x14, GetOffset(in s, in s.ContentOwnerIdMax));
        Assert.Equal(0x1C, GetOffset(in s, in s.SaveDataOwnerIdMin));
        Assert.Equal(0x24, GetOffset(in s, in s.SaveDataOwnerIdMax));
    }

    [Fact]
    public static void AccessControlDataHeader_Layout()
    {
        var s = new AccessControlDataHeader();

        Assert.Equal(0x1C, Unsafe.SizeOf<AccessControlDataHeader>());

        Assert.Equal(0x00, GetOffset(in s, in s.Version));
        Assert.Equal(0x04, GetOffset(in s, in s.AccessFlags));
        Assert.Equal(0x0C, GetOffset(in s, in s.ContentOwnerInfoOffset));
        Assert.Equal(0x10, GetOffset(in s, in s.ContentOwnerInfoSize));
        Assert.Equal(0x14, GetOffset(in s, in s.SaveDataOwnerInfoOffset));
        Assert.Equal(0x18, GetOffset(in s, in s.SaveDataOwnerInfoSize));
    }

    [Fact]
    public static void Accessibility_Layout()
    {
        Assert.Equal(1, Unsafe.SizeOf<Accessibility>());
    }

    [Fact]
    public static void FspPath_Layout()
    {
        var s = new FspPath();

        Assert.Equal(0x301, Unsafe.SizeOf<FspPath>());

        Assert.Equal(0x00, GetOffset(in s, in s.Str[0]));
    }

    [Fact]
    public static void Path_Layout()
    {
        var s = new Path();

        Assert.Equal(0x301, Unsafe.SizeOf<Path>());

        Assert.Equal(0x00, GetOffset(in s, in s.Str[0]));
    }

    [Fact]
    public static void StorageDeviceHandle_Layout()
    {
        var s = new StorageDeviceHandle();

        Assert.Equal(0x10, Unsafe.SizeOf<StorageDeviceHandle>());

        Assert.Equal(0x0, GetOffset(in s, in s.Value));
        Assert.Equal(0x4, GetOffset(in s, in s.PortId));
        Assert.Equal(0x5, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void SaveDataIndexerValue_Layout()
    {
        var s = new SaveDataIndexerValue();

        Assert.Equal(0x40, Unsafe.SizeOf<SaveDataIndexerValue>());

        Assert.Equal(0x00, GetOffset(in s, in s.SaveDataId));
        Assert.Equal(0x08, GetOffset(in s, in s.Size));
        Assert.Equal(0x10, GetOffset(in s, in s.Field10));
        Assert.Equal(0x18, GetOffset(in s, in s.SpaceId));
        Assert.Equal(0x19, GetOffset(in s, in s.State));
        Assert.Equal(0x1A, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void InitialDataVersion1Detail_Layout()
    {
        var s = new InitialDataVersion1Detail();

        Assert.Equal(0x280, Unsafe.SizeOf<InitialDataVersion1Detail>());

        Assert.Equal(0x000, GetOffset(in s, in s.DataContent));
        Assert.Equal(0x270, GetOffset(in s, in s.Mac));
    }

    [Fact]
    public static void InitialDataVersion1Detail_Content_Layout()
    {
        var s = new InitialDataVersion1Detail.Content();

        Assert.Equal(0x270, Unsafe.SizeOf<InitialDataVersion1Detail.Content>());

        Assert.Equal(0x000, GetOffset(in s, in s.ExtraData));
        Assert.Equal(0x200, GetOffset(in s, in s.Version));
        Assert.Equal(0x204, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void InitialDataVersion2Detail_Layout()
    {
        var s = new InitialDataVersion2Detail();

        Assert.Equal(0x2000, Unsafe.SizeOf<InitialDataVersion2Detail>());

        Assert.Equal(0x0000, GetOffset(in s, in s.GcmStreamHeader));
        Assert.Equal(0x0020, GetOffset(in s, in s.DataContent));
        Assert.Equal(0x1FF0, GetOffset(in s, in s.GcmStreamTail));
    }

    [Fact]
    public static void InitialDataVersion2Detail_Content_Layout()
    {
        var s = new InitialDataVersion2Detail.Content();

        Assert.Equal(0x1FD0, Unsafe.SizeOf<InitialDataVersion2Detail.Content>());

        Assert.Equal(0x0000, GetOffset(in s, in s.Signature));
        Assert.Equal(0x0004, GetOffset(in s, in s.Version));
        Assert.Equal(0x0008, GetOffset(in s, in s.ExtraData));
        Assert.Equal(0x0208, GetOffset(in s, in s.DivisionCount));
        Assert.Equal(0x0210, GetOffset(in s, in s.DivisionAlignment));
        Assert.Equal(0x0218, GetOffset(in s, in s.ChunkHashes));
        Assert.Equal(0x0A18, GetOffset(in s, in s.ChunkSizes));
        Assert.Equal(0x0C18, GetOffset(in s, in s.ChunkMacs));
        Assert.Equal(0x1018, GetOffset(in s, in s.TotalChunkSize));
        Assert.Equal(0x1020, GetOffset(in s, in s.InitialDataAad));
        Assert.Equal(0x1040, GetOffset(in s, in s.HashSalt));
        Assert.Equal(0x1060, GetOffset(in s, in s.ShortHashes));
        Assert.Equal(0x1260, GetOffset(in s, in s.IsIntegritySeedEnabled));
        Assert.Equal(0x1261, GetOffset(in s, in s.HashAlgorithmType));
        Assert.Equal(0x1262, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void SaveDataTransferManagerForSaveDataRepair_KeyPackageV0_Layout()
    {
        var s = new SaveDataTransferManagerForSaveDataRepair.KeyPackageV0();

        Assert.Equal(0x280, Unsafe.SizeOf<SaveDataTransferManagerForSaveDataRepair.KeyPackageV0>());

        Assert.Equal(0x000, GetOffset(in s, in s.Version));
        Assert.Equal(0x004, GetOffset(in s, in s.Reserved4));
        Assert.Equal(0x008, GetOffset(in s, in s.KeyGeneration));
        Assert.Equal(0x009, GetOffset(in s, in s.Reserved9));
        Assert.Equal(0x010, GetOffset(in s, in s.Iv));
        Assert.Equal(0x020, GetOffset(in s, in s.Mac));
        Assert.Equal(0x030, GetOffset(in s, in s.Reserved30));
        Assert.Equal(0x080, GetOffset(in s, in s.PackageContent));
        Assert.Equal(0x180, GetOffset(in s, in s.Signature));
    }

    [Fact]
    public static void SaveDataTransferManagerForSaveDataRepair_KeyPackageV0_Content_Layout()
    {
        var s = new SaveDataTransferManagerForSaveDataRepair.KeyPackageV0.Content();

        Assert.Equal(0x100, Unsafe.SizeOf<SaveDataTransferManagerForSaveDataRepair.KeyPackageV0.Content>());

        Assert.Equal(0x00, GetOffset(in s, in s.InitialDataMacBeforeRepair));
        Assert.Equal(0x10, GetOffset(in s, in s.KeyGenerationBeforeRepair));
        Assert.Equal(0x18, GetOffset(in s, in s.InitialDataMacAfterRepair));
        Assert.Equal(0x28, GetOffset(in s, in s.KeyGenerationAfterRepair));
        Assert.Equal(0x30, GetOffset(in s, in s.Keys));
        Assert.Equal(0x50, GetOffset(in s, in s.Iv));
        Assert.Equal(0x60, GetOffset(in s, in s.Mac));
        Assert.Equal(0x70, GetOffset(in s, in s.Reserved70));
        Assert.Equal(0x78, GetOffset(in s, in s.ChallengeData));
        Assert.Equal(0x88, GetOffset(in s, in s.Reserved88));
    }

    [Fact]
    public static void KeySeedPackageV0_Layout()
    {
        var s = new KeySeedPackageV0();

        Assert.Equal(0x200, Unsafe.SizeOf<KeySeedPackageV0>());

        Assert.Equal(0x000, GetOffset(in s, in s.Version));
        Assert.Equal(0x004, GetOffset(in s, in s.Reserved4));
        Assert.Equal(0x008, GetOffset(in s, in s.KeyGeneration));
        Assert.Equal(0x009, GetOffset(in s, in s.Reserved9));
        Assert.Equal(0x010, GetOffset(in s, in s.Iv));
        Assert.Equal(0x020, GetOffset(in s, in s.Mac));
        Assert.Equal(0x030, GetOffset(in s, in s.Reserved30));
        Assert.Equal(0x080, GetOffset(in s, in s.Signature));
        Assert.Equal(0x180, GetOffset(in s, in s.PackageContent));
    }

    [Fact]
    public static void KeySeedPackageV0_Content_Layout()
    {
        var s = new KeySeedPackageV0.Content();

        Assert.Equal(0x80, Unsafe.SizeOf<KeySeedPackageV0.Content>());

        Assert.Equal(0x00, GetOffset(in s, in s.Unknown));
        Assert.Equal(0x10, GetOffset(in s, in s.TransferKeySeed));
        Assert.Equal(0x20, GetOffset(in s, in s.TransferInitialDataMac));
        Assert.Equal(0x30, GetOffset(in s, in s.Challenge));
        Assert.Equal(0x40, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void ExportContextDetail_Layout()
    {
        var s = new ExportContextDetail();

        Assert.Equal(0x4000, Unsafe.SizeOf<ExportContextDetail>());

        Assert.Equal(0x0000, GetOffset(in s, in s.GcmStreamHeader));
        Assert.Equal(0x0020, GetOffset(in s, in s.DataContent));
        Assert.Equal(0x3FF0, GetOffset(in s, in s.GcmStreamTail));
    }

    [Fact]
    public static void ExportContextDetail_Content_Layout()
    {
        var s = new ExportContextDetail.Content();

        Assert.Equal(0x3FD0, Unsafe.SizeOf<ExportContextDetail.Content>());

        Assert.Equal(0x0000, GetOffset(in s, in s.Magic));
        Assert.Equal(0x0004, GetOffset(in s, in s.Unk));
        Assert.Equal(0x0008, GetOffset(in s, in s.IsDiffExport));
        Assert.Equal(0x0010, GetOffset(in s, in s.ChunkSizes));
        Assert.Equal(0x0210, GetOffset(in s, in s.ChunkMacs));
        Assert.Equal(0x0610, GetOffset(in s, in s.IsChunkComplete));
        Assert.Equal(0x0650, GetOffset(in s, in s.InitialData));
        Assert.Equal(0x2650, GetOffset(in s, in s.SpaceId));
        Assert.Equal(0x2658, GetOffset(in s, in s.SaveDataId));
        Assert.Equal(0x2660, GetOffset(in s, in s.CommitId));
        Assert.Equal(0x2668, GetOffset(in s, in s.DivisionCount));
        Assert.Equal(0x2670, GetOffset(in s, in s.DivisionAlignment));
        Assert.Equal(0x2678, GetOffset(in s, in s.InitialDataAad));
        Assert.Equal(0x2698, GetOffset(in s, in s.IsInitialDataComplete));
        Assert.Equal(0x2699, GetOffset(in s, in s.ChunkIvs));
        Assert.Equal(0x2A99, GetOffset(in s, in s.OuterInitialDataIv));
        Assert.Equal(0x2AA9, GetOffset(in s, in s.KeySeed));
        Assert.Equal(0x2AB9, GetOffset(in s, in s.ThumbnailHash));
        Assert.Equal(0x2AD9, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void ImportContextDetail_Layout()
    {
        var s = new ImportContextDetail();

        Assert.Equal(0x4000, Unsafe.SizeOf<ImportContextDetail>());

        Assert.Equal(0x0000, GetOffset(in s, in s.GcmStreamHeader));
        Assert.Equal(0x0020, GetOffset(in s, in s.DataContent));
        Assert.Equal(0x3FF0, GetOffset(in s, in s.GcmStreamTail));
    }

    [Fact]
    public static void ImportContextDetail_Content_Layout()
    {
        var s = new ImportContextDetail.Content();

        Assert.Equal(0x3FD0, Unsafe.SizeOf<ImportContextDetail.Content>());

        Assert.Equal(0x0000, GetOffset(in s, in s.Magic));
        Assert.Equal(0x0004, GetOffset(in s, in s.Unk));
        Assert.Equal(0x0008, GetOffset(in s, in s.Mode));
        Assert.Equal(0x0010, GetOffset(in s, in s.InitialData));
        Assert.Equal(0x2010, GetOffset(in s, in s.SpaceId));
        Assert.Equal(0x2018, GetOffset(in s, in s.SourceSaveDataId));
        Assert.Equal(0x2020, GetOffset(in s, in s.DestinationSaveDataId));
        Assert.Equal(0x2028, GetOffset(in s, in s.SourceZeroCommitId));
        Assert.Equal(0x2030, GetOffset(in s, in s.DestinationZeroCommitId));
        Assert.Equal(0x2038, GetOffset(in s, in s.UserId));
        Assert.Equal(0x2048, GetOffset(in s, in s.TimeStamp));
        Assert.Equal(0x2050, GetOffset(in s, in s.ReportInfo));
        Assert.Equal(0x2070, GetOffset(in s, in s.ThumbnailHash));
        Assert.Equal(0x2090, GetOffset(in s, in s.Reserved));
    }
}