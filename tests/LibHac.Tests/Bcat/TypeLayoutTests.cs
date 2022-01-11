using System.Runtime.CompilerServices;
using LibHac.Bcat;
using LibHac.Bcat.Impl.Service.Core;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Bcat;

public class TypeLayoutTests
{
    [Fact]
    public static void Digest_Layout()
    {
        var s = new Digest();

        Assert.Equal(0x10, Unsafe.SizeOf<Digest>());

        Assert.Equal(0x0, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void DirectoryName_Layout()
    {
        var s = new DirectoryName();

        Assert.Equal(0x20, Unsafe.SizeOf<DirectoryName>());

        Assert.Equal(0x0, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void FileName_Layout()
    {
        var s = new FileName();

        Assert.Equal(0x20, Unsafe.SizeOf<FileName>());

        Assert.Equal(0x0, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void DeliveryCacheDirectoryEntry_Layout()
    {
        var s = new DeliveryCacheDirectoryEntry();

        Assert.Equal(0x38, Unsafe.SizeOf<DeliveryCacheDirectoryEntry>());

        Assert.Equal(0x00, GetOffset(in s, in s.Name));
        Assert.Equal(0x20, GetOffset(in s, in s.Size));
        Assert.Equal(0x28, GetOffset(in s, in s.Digest));
    }

    [Fact]
    public static void DeliveryCacheDirectoryMetaEntry_Layout()
    {
        var s = new DeliveryCacheDirectoryMetaEntry();

        Assert.Equal(0x40, Unsafe.SizeOf<DeliveryCacheDirectoryMetaEntry>());

        Assert.Equal(0x00, GetOffset(in s, in s.Name));
        Assert.Equal(0x20, GetOffset(in s, in s.Digest));
        Assert.Equal(0x30, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void DeliveryCacheFileMetaEntry_Layout()
    {
        var s = new DeliveryCacheFileMetaEntry();

        Assert.Equal(0x80, Unsafe.SizeOf<DeliveryCacheFileMetaEntry>());

        Assert.Equal(0x00, GetOffset(in s, in s.Name));
        Assert.Equal(0x20, GetOffset(in s, in s.Id));
        Assert.Equal(0x28, GetOffset(in s, in s.Size));
        Assert.Equal(0x30, GetOffset(in s, in s.Digest));
        Assert.Equal(0x40, GetOffset(in s, in s.Reserved));
    }
}