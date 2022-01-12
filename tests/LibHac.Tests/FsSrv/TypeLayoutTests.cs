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
}