using System.Runtime.CompilerServices;
using LibHac.Kernel;
using Xunit;
using static LibHac.Kernel.InitialProcessBinaryReader;
using static LibHac.Kernel.KipHeader;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Kernel;

public class TypeLayoutTests
{
    [Fact]
    public static void IniHeader_Layout()
    {
        var s = new IniHeader();

        Assert.Equal(0x10, Unsafe.SizeOf<IniHeader>());

        Assert.Equal(0x0, GetOffset(in s, in s.Magic));
        Assert.Equal(0x4, GetOffset(in s, in s.Size));
        Assert.Equal(0x8, GetOffset(in s, in s.ProcessCount));
        Assert.Equal(0xC, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void KipHeader_Layout()
    {
        var s = new KipHeader();

        Assert.Equal(0x100, Unsafe.SizeOf<KipHeader>());

        Assert.Equal(0x00, GetOffset(in s, in s.Magic));
        Assert.Equal(0x04, GetOffset(in s, in s.Name));
        Assert.Equal(0x10, GetOffset(in s, in s.ProgramId));
        Assert.Equal(0x18, GetOffset(in s, in s.Version));
        Assert.Equal(0x1C, GetOffset(in s, in s.Priority));
        Assert.Equal(0x1D, GetOffset(in s, in s.IdealCoreId));
        Assert.Equal(0x1F, GetOffset(in s, in s.Flags));
        Assert.Equal(0x20, GetOffset(in s, in s.TextMemoryOffset));
        Assert.Equal(0x24, GetOffset(in s, in s.TextSize));
        Assert.Equal(0x28, GetOffset(in s, in s.TextFileSize));
        Assert.Equal(0x2C, GetOffset(in s, in s.AffinityMask));
        Assert.Equal(0x30, GetOffset(in s, in s.RoMemoryOffset));
        Assert.Equal(0x34, GetOffset(in s, in s.RoSize));
        Assert.Equal(0x38, GetOffset(in s, in s.RoFileSize));
        Assert.Equal(0x3C, GetOffset(in s, in s.StackSize));
        Assert.Equal(0x40, GetOffset(in s, in s.DataMemoryOffset));
        Assert.Equal(0x44, GetOffset(in s, in s.DataSize));
        Assert.Equal(0x48, GetOffset(in s, in s.DataFileSize));
        Assert.Equal(0x50, GetOffset(in s, in s.BssMemoryOffset));
        Assert.Equal(0x54, GetOffset(in s, in s.BssSize));
        Assert.Equal(0x58, GetOffset(in s, in s.BssFileSize));
        Assert.Equal(0x80, GetOffset(in s, in s.Capabilities));

        Assert.Equal(0x20, GetOffset(in s, in s.Segments[0]));
    }

    [Fact]
    public static void KipSegmentHeader_Layout()
    {
        var s = new SegmentHeader();

        Assert.Equal(0x10, Unsafe.SizeOf<SegmentHeader>());

        Assert.Equal(0x0, GetOffset(in s, in s.MemoryOffset));
        Assert.Equal(0x4, GetOffset(in s, in s.Size));
        Assert.Equal(0x8, GetOffset(in s, in s.FileSize));
    }
}