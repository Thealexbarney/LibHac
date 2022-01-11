using System.Runtime.CompilerServices;
using LibHac.Loader;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Loader;

public class TypeLayoutTests
{
    [Fact]
    public static void NsoHeader_Layout()
    {
        var s = new NsoHeader();

        Assert.Equal(0x100, Unsafe.SizeOf<NsoHeader>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.Version));
        Assert.Equal(0x08, GetOffset(in s, in s.Reserved08));
        Assert.Equal(0x0C, GetOffset(in s, in s.Flags));

        Assert.Equal(0x10, GetOffset(in s, in s.TextFileOffset));
        Assert.Equal(0x14, GetOffset(in s, in s.TextMemoryOffset));
        Assert.Equal(0x18, GetOffset(in s, in s.TextSize));

        Assert.Equal(0x1C, GetOffset(in s, in s.ModuleNameOffset));

        Assert.Equal(0x20, GetOffset(in s, in s.RoFileOffset));
        Assert.Equal(0x24, GetOffset(in s, in s.RoMemoryOffset));
        Assert.Equal(0x28, GetOffset(in s, in s.RoSize));

        Assert.Equal(0x2C, GetOffset(in s, in s.ModuleNameSize));

        Assert.Equal(0x30, GetOffset(in s, in s.DataFileOffset));
        Assert.Equal(0x34, GetOffset(in s, in s.DataMemoryOffset));
        Assert.Equal(0x38, GetOffset(in s, in s.DataSize));

        Assert.Equal(0x3C, GetOffset(in s, in s.BssSize));

        Assert.Equal(0x40, GetOffset(in s, in s.ModuleId));

        Assert.Equal(0x60, GetOffset(in s, in s.TextFileSize));
        Assert.Equal(0x64, GetOffset(in s, in s.RoFileSize));
        Assert.Equal(0x68, GetOffset(in s, in s.DataFileSize));

        Assert.Equal(0x6C, GetOffset(in s, in s.Reserved6C));

        Assert.Equal(0x88, GetOffset(in s, in s.ApiInfoOffset));
        Assert.Equal(0x8C, GetOffset(in s, in s.ApiInfoSize));
        Assert.Equal(0x90, GetOffset(in s, in s.DynStrOffset));
        Assert.Equal(0x94, GetOffset(in s, in s.DynStrSize));
        Assert.Equal(0x98, GetOffset(in s, in s.DynSymOffset));
        Assert.Equal(0x9C, GetOffset(in s, in s.DynSymSize));

        Assert.Equal(0xA0, GetOffset(in s, in s.TextHash));
        Assert.Equal(0xC0, GetOffset(in s, in s.RoHash));
        Assert.Equal(0xE0, GetOffset(in s, in s.DataHash));
    }

    [Fact]
    public static void Meta_layout()
    {
        var s = new Meta();

        Assert.Equal(0x80, Unsafe.SizeOf<Meta>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.SignatureKeyGeneration));
        Assert.Equal(0x08, GetOffset(in s, in s.Reserved08));
        Assert.Equal(0x0C, GetOffset(in s, in s.Flags));
        Assert.Equal(0x0D, GetOffset(in s, in s.Reserved0D));
        Assert.Equal(0x0E, GetOffset(in s, in s.MainThreadPriority));
        Assert.Equal(0x0F, GetOffset(in s, in s.DefaultCpuId));
        Assert.Equal(0x10, GetOffset(in s, in s.Reserved10));
        Assert.Equal(0x14, GetOffset(in s, in s.SystemResourceSize));
        Assert.Equal(0x18, GetOffset(in s, in s.Version));
        Assert.Equal(0x1C, GetOffset(in s, in s.MainThreadStackSize));
        Assert.Equal(0x20, GetOffset(in s, in s.ProgramName));
        Assert.Equal(0x30, GetOffset(in s, in s.ProductCode));
        Assert.Equal(0x40, GetOffset(in s, in s.Reserved40));
        Assert.Equal(0x70, GetOffset(in s, in s.AciOffset));
        Assert.Equal(0x74, GetOffset(in s, in s.AciSize));
        Assert.Equal(0x78, GetOffset(in s, in s.AcidOffset));
        Assert.Equal(0x7C, GetOffset(in s, in s.AcidSize));
    }

    [Fact]
    public static void AciHeader_Layout()
    {
        var s = new AciHeader();

        Assert.Equal(0x40, Unsafe.SizeOf<AciHeader>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.Reserved04));
        Assert.Equal(0x10, GetOffset(in s, in s.ProgramId));
        Assert.Equal(0x18, GetOffset(in s, in s.Reserved18));
        Assert.Equal(0x20, GetOffset(in s, in s.FsAccessControlOffset));
        Assert.Equal(0x24, GetOffset(in s, in s.FsAccessControlSize));
        Assert.Equal(0x28, GetOffset(in s, in s.ServiceAccessControlOffset));
        Assert.Equal(0x2C, GetOffset(in s, in s.ServiceAccessControlSize));
        Assert.Equal(0x30, GetOffset(in s, in s.KernelCapabilityOffset));
        Assert.Equal(0x34, GetOffset(in s, in s.KernelCapabilitySize));
        Assert.Equal(0x38, GetOffset(in s, in s.Reserved38));
    }

    [Fact]
    public static void AcidHeaderData_Layout()
    {
        var s = new AcidHeaderData();

        Assert.Equal(0x240, Unsafe.SizeOf<AcidHeaderData>());

        Assert.Equal(0x000, GetOffset(in s, in s.Signature));
        Assert.Equal(0x100, GetOffset(in s, in s.Modulus));
        Assert.Equal(0x200, GetOffset(in s, in s.Magic));
        Assert.Equal(0x204, GetOffset(in s, in s.Size));
        Assert.Equal(0x208, GetOffset(in s, in s.Version));
        Assert.Equal(0x20C, GetOffset(in s, in s.Flags));
        Assert.Equal(0x210, GetOffset(in s, in s.ProgramIdMin));
        Assert.Equal(0x218, GetOffset(in s, in s.ProgramIdMax));
        Assert.Equal(0x220, GetOffset(in s, in s.FsAccessControlOffset));
        Assert.Equal(0x224, GetOffset(in s, in s.FsAccessControlSize));
        Assert.Equal(0x228, GetOffset(in s, in s.ServiceAccessControlOffset));
        Assert.Equal(0x22C, GetOffset(in s, in s.ServiceAccessControlSize));
        Assert.Equal(0x230, GetOffset(in s, in s.KernelCapabilityOffset));
        Assert.Equal(0x234, GetOffset(in s, in s.KernelCapabilitySize));
        Assert.Equal(0x238, GetOffset(in s, in s.Reserved238));
    }
}