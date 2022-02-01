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
}