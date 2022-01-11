using System.Runtime.CompilerServices;
using LibHac.Boot;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Boot;

public class TypeLayoutTests
{
    [Fact]
    public static void EncryptedKeyBlob_Layout()
    {
        var s = new EncryptedKeyBlob();

        Assert.Equal(0xB0, Unsafe.SizeOf<EncryptedKeyBlob>());

        Assert.Equal(0x00, GetOffset(in s, in s.Cmac));
        Assert.Equal(0x10, GetOffset(in s, in s.Counter));
        Assert.Equal(0x20, GetOffset(in s, in s.Payload));
    }

    [Fact]
    public static void KeyBlob_Layout()
    {
        var s = new KeyBlob();

        Assert.Equal(0x90, Unsafe.SizeOf<KeyBlob>());

        Assert.Equal(0x00, GetOffset(in s, in s.MasterKek));
        Assert.Equal(0x10, GetOffset(in s, in s.Unused));
        Assert.Equal(0x80, GetOffset(in s, in s.Package1Key));
    }

    [Fact]
    public static void Package1MarikoOemHeader_Layout()
    {
        var s = new Package1MarikoOemHeader();

        Assert.Equal(0x170, Unsafe.SizeOf<Package1MarikoOemHeader>());

        Assert.Equal(0x000, GetOffset(in s, in s.AesMac));
        Assert.Equal(0x010, GetOffset(in s, in s.RsaSig));
        Assert.Equal(0x110, GetOffset(in s, in s.Salt));
        Assert.Equal(0x130, GetOffset(in s, in s.Hash));
        Assert.Equal(0x150, GetOffset(in s, in s.Version));
        Assert.Equal(0x154, GetOffset(in s, in s.Size));
        Assert.Equal(0x158, GetOffset(in s, in s.LoadAddress));
        Assert.Equal(0x15C, GetOffset(in s, in s.EntryPoint));
        Assert.Equal(0x160, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void Package1MetaData_Layout()
    {
        var s = new Package1MetaData();

        Assert.Equal(0x20, Unsafe.SizeOf<Package1MetaData>());

        Assert.Equal(0x00, GetOffset(in s, in s.LoaderHash));
        Assert.Equal(0x04, GetOffset(in s, in s.SecureMonitorHash));
        Assert.Equal(0x08, GetOffset(in s, in s.BootloaderHash));
        Assert.Equal(0x0C, GetOffset(in s, in s.Reserved));
        Assert.Equal(0x10, GetOffset(in s, in s.BuildDate.Value[0]));
        Assert.Equal(0x1E, GetOffset(in s, in s.KeyGeneration));
        Assert.Equal(0x1F, GetOffset(in s, in s.Version));

        Assert.Equal(0x10, GetOffset(in s, in s.Iv[0]));
    }

    [Fact]
    public static void Package1Stage1Footer_Layout()
    {
        var s = new Package1Stage1Footer();

        Assert.Equal(0x20, Unsafe.SizeOf<Package1Stage1Footer>());

        Assert.Equal(0x00, GetOffset(in s, in s.Pk11Size));
        Assert.Equal(0x04, GetOffset(in s, in s.Reserved));
        Assert.Equal(0x10, GetOffset(in s, in s.Iv));
    }

    [Fact]
    public static void Package1Pk11Header_Layout()
    {
        var s = new Package1Pk11Header();

        Assert.Equal(0x20, Unsafe.SizeOf<Package1Pk11Header>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.WarmBootSize));
        Assert.Equal(0x08, GetOffset(in s, in s.WarmBootOffset));
        Assert.Equal(0x0C, GetOffset(in s, in s.Reserved));
        Assert.Equal(0x10, GetOffset(in s, in s.BootloaderSize));
        Assert.Equal(0x14, GetOffset(in s, in s.BootloaderOffset));
        Assert.Equal(0x18, GetOffset(in s, in s.SecureMonitorSize));
        Assert.Equal(0x1C, GetOffset(in s, in s.SecureMonitorOffset));
    }

    [Fact]
    public static void Package2Header_Layout()
    {
        var s = new Package2Header();

        Assert.Equal(0x200, Unsafe.SizeOf<Package2Header>());

        Assert.Equal(0x000, GetOffset(in s, in s.Signature));
        Assert.Equal(0x100, GetOffset(in s, in s.Meta));
    }

    [Fact]
    public static void Package2Meta_Layout()
    {
        var s = new Package2Meta();

        Assert.Equal(0x100, Unsafe.SizeOf<Package2Meta>());

        Assert.Equal(0x00, GetOffset(in s, in s.HeaderIv));
        Assert.Equal(0x10, GetOffset(in s, in s.PayloadIvs));
        Assert.Equal(0x40, GetOffset(in s, in s.Padding40));
        Assert.Equal(0x50, GetOffset(in s, in s.Magic));
        Assert.Equal(0x54, GetOffset(in s, in s.EntryPoint));
        Assert.Equal(0x5C, GetOffset(in s, in s.Package2Version));
        Assert.Equal(0x58, GetOffset(in s, in s.Padding58));
        Assert.Equal(0x5D, GetOffset(in s, in s.BootloaderVersion));
        Assert.Equal(0x60, GetOffset(in s, in s.PayloadSizes));
        Assert.Equal(0x6C, GetOffset(in s, in s.Padding6C));
        Assert.Equal(0x70, GetOffset(in s, in s.PayloadOffsets));
        Assert.Equal(0x7C, GetOffset(in s, in s.Padding7C));
        Assert.Equal(0x80, GetOffset(in s, in s.PayloadHashes));
        Assert.Equal(0xE0, GetOffset(in s, in s.PaddingE0));
    }
}