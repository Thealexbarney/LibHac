using System.Runtime.CompilerServices;
using LibHac.Gc;
using LibHac.Gc.Impl;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Gc;

public class TypeLayoutTests
{
    [Fact]
    public static void GameCardStatus_Layout()
    {
        var s = new GameCardStatus();

        Assert.Equal(0x70, Unsafe.SizeOf<GameCardStatus>());

        Assert.Equal(0x00, GetOffset(in s, in s.PartitionFsHeaderHash));
        Assert.Equal(0x20, GetOffset(in s, in s.PackageId));
        Assert.Equal(0x28, GetOffset(in s, in s.Size));
        Assert.Equal(0x30, GetOffset(in s, in s.PartitionFsHeaderOffset));
        Assert.Equal(0x38, GetOffset(in s, in s.PartitionFsHeaderSize));
        Assert.Equal(0x40, GetOffset(in s, in s.SecureAreaOffset));
        Assert.Equal(0x48, GetOffset(in s, in s.SecureAreaSize));
        Assert.Equal(0x50, GetOffset(in s, in s.UpdatePartitionVersion));
        Assert.Equal(0x58, GetOffset(in s, in s.UpdatePartitionId));
        Assert.Equal(0x60, GetOffset(in s, in s.CompatibilityType));
        Assert.Equal(0x61, GetOffset(in s, in s.Reserved61));
        Assert.Equal(0x64, GetOffset(in s, in s.GameCardAttribute));
        Assert.Equal(0x65, GetOffset(in s, in s.Reserved65));
    }

    [Fact]
    public static void RmaInformation_Layout()
    {
        var s = new RmaInformation();

        Assert.Equal(0x200, Unsafe.SizeOf<RmaInformation>());

        Assert.Equal(0x00, GetOffset(in s, in s.Data));
    }

    [Fact]
    public static void GameCardIdSet_Layout()
    {
        var s = new GameCardIdSet();

        Assert.Equal(12, Unsafe.SizeOf<GameCardIdSet>());

        Assert.Equal(0, GetOffset(in s, in s.Id1));
        Assert.Equal(4, GetOffset(in s, in s.Id2));
        Assert.Equal(8, GetOffset(in s, in s.Id3));
    }

    [Fact]
    public static void CardId1_Layout()
    {
        var s = new CardId1();

        Assert.Equal(4, Unsafe.SizeOf<CardId1>());

        Assert.Equal(0, GetOffset(in s, in s.MakerCode));
        Assert.Equal(1, GetOffset(in s, in s.MemoryCapacity));
        Assert.Equal(2, GetOffset(in s, in s.Reserved));
        Assert.Equal(3, GetOffset(in s, in s.MemoryType));
    }

    [Fact]
    public static void CardId2_Layout()
    {
        var s = new CardId2();

        Assert.Equal(4, Unsafe.SizeOf<CardId2>());

        Assert.Equal(0, GetOffset(in s, in s.CardSecurityNumber));
        Assert.Equal(1, GetOffset(in s, in s.CardType));
        Assert.Equal(2, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void CardId3_Layout()
    {
        var s = new CardId3();

        Assert.Equal(4, Unsafe.SizeOf<CardId3>());

        Assert.Equal(0, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void CardInitialDataPayload_Layout()
    {
        var s = new CardInitialDataPayload();

        Assert.Equal(0x3C, Unsafe.SizeOf<CardInitialDataPayload>());

        Assert.Equal(0x00, GetOffset(in s, in s.PackageId));
        Assert.Equal(0x08, GetOffset(in s, in s.Reserved));
        Assert.Equal(0x10, GetOffset(in s, in s.AuthData));
        Assert.Equal(0x20, GetOffset(in s, in s.AuthMac));
        Assert.Equal(0x30, GetOffset(in s, in s.AuthNonce));
    }

    [Fact]
    public static void CardInitialData_Layout()
    {
        var s = new CardInitialData();

        Assert.Equal(0x200, Unsafe.SizeOf<CardInitialData>());

        Assert.Equal(0x00, GetOffset(in s, in s.Payload));
        Assert.Equal(0x3C, GetOffset(in s, in s.Padding));
    }

    [Fact]
    public static void CardHeaderEncryptedData_Layout()
    {
        var s = new CardHeaderEncryptedData();

        Assert.Equal(0x70, Unsafe.SizeOf<CardHeaderEncryptedData>());

        Assert.Equal(0x00, GetOffset(in s, in s.FwVersion));
        Assert.Equal(0x08, GetOffset(in s, in s.AccCtrl1));
        Assert.Equal(0x0C, GetOffset(in s, in s.Wait1TimeRead));
        Assert.Equal(0x10, GetOffset(in s, in s.Wait2TimeRead));
        Assert.Equal(0x14, GetOffset(in s, in s.Wait1TimeWrite));
        Assert.Equal(0x18, GetOffset(in s, in s.Wait2TimeWrite));
        Assert.Equal(0x1C, GetOffset(in s, in s.FwMode));
        Assert.Equal(0x20, GetOffset(in s, in s.CupVersion));
        Assert.Equal(0x24, GetOffset(in s, in s.CompatibilityType));
        Assert.Equal(0x25, GetOffset(in s, in s.Reserved25));
        Assert.Equal(0x26, GetOffset(in s, in s.Reserved26));
        Assert.Equal(0x27, GetOffset(in s, in s.Reserved27));
        Assert.Equal(0x28, GetOffset(in s, in s.UppHash));
        Assert.Equal(0x30, GetOffset(in s, in s.CupId));
        Assert.Equal(0x38, GetOffset(in s, in s.Reserved38));
    }

    [Fact]
    public static void CardHeader_Layout()
    {
        var s = new CardHeader();

        Assert.Equal(0x100, Unsafe.SizeOf<CardHeader>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.RomAreaStartPage));
        Assert.Equal(0x08, GetOffset(in s, in s.BackupAreaStartPage));
        Assert.Equal(0x0C, GetOffset(in s, in s.KeyIndex));
        Assert.Equal(0x0D, GetOffset(in s, in s.RomSize));
        Assert.Equal(0x0E, GetOffset(in s, in s.Version));
        Assert.Equal(0x0F, GetOffset(in s, in s.Flags));
        Assert.Equal(0x10, GetOffset(in s, in s.PackageId));
        Assert.Equal(0x18, GetOffset(in s, in s.ValidDataEndPage));
        Assert.Equal(0x1C, GetOffset(in s, in s.Reserved11C));
        Assert.Equal(0x20, GetOffset(in s, in s.Iv));
        Assert.Equal(0x30, GetOffset(in s, in s.PartitionFsHeaderAddress));
        Assert.Equal(0x38, GetOffset(in s, in s.PartitionFsHeaderSize));
        Assert.Equal(0x40, GetOffset(in s, in s.PartitionFsHeaderHash));
        Assert.Equal(0x60, GetOffset(in s, in s.InitialDataHash));
        Assert.Equal(0x80, GetOffset(in s, in s.SelSec));
        Assert.Equal(0x84, GetOffset(in s, in s.SelT1Key));
        Assert.Equal(0x88, GetOffset(in s, in s.SelKey));
        Assert.Equal(0x8C, GetOffset(in s, in s.LimAreaPage));
        Assert.Equal(0x90, GetOffset(in s, in s.EncryptedData));
    }

    [Fact]
    public static void CardHeaderWithSignature_Layout()
    {
        var s = new CardHeaderWithSignature();

        Assert.Equal(0x200, Unsafe.SizeOf<CardHeaderWithSignature>());

        Assert.Equal(0x000, GetOffset(in s, in s.Signature));
        Assert.Equal(0x100, GetOffset(in s, in s.Data));
    }

    [Fact]
    public static void T1CardCertificate_Layout()
    {
        var s = new T1CardCertificate();

        Assert.Equal(0x400, Unsafe.SizeOf<T1CardCertificate>());

        Assert.Equal(0x000, GetOffset(in s, in s.Signature));
        Assert.Equal(0x100, GetOffset(in s, in s.Magic));
        Assert.Equal(0x104, GetOffset(in s, in s.Version));
        Assert.Equal(0x108, GetOffset(in s, in s.KekIndex));
        Assert.Equal(0x109, GetOffset(in s, in s.Flags));
        Assert.Equal(0x110, GetOffset(in s, in s.T1CardDeviceId));
        Assert.Equal(0x120, GetOffset(in s, in s.Iv));
        Assert.Equal(0x130, GetOffset(in s, in s.HwKey));
        Assert.Equal(0x140, GetOffset(in s, in s.Reserved));
        Assert.Equal(0x200, GetOffset(in s, in s.Padding));
    }

    [Fact]
    public static void Ca10Certificate_Layout()
    {
        var s = new Ca10Certificate();

        Assert.Equal(0x400, Unsafe.SizeOf<Ca10Certificate>());

        Assert.Equal(0x000, GetOffset(in s, in s.Signature));
        Assert.Equal(0x100, GetOffset(in s, in s.Unk100));
        Assert.Equal(0x130, GetOffset(in s, in s.Modulus));
        Assert.Equal(0x230, GetOffset(in s, in s.Unk230));
    }
}