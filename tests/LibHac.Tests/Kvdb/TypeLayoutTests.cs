using System.Runtime.CompilerServices;
using LibHac.Kvdb;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Kvdb;

public class TypeLayoutTests
{
    [Fact]
    public static void KeyValueArchiveHeader_Layout()
    {
        var s = new KeyValueArchiveHeader();

        Assert.Equal(0xC, Unsafe.SizeOf<KeyValueArchiveHeader>());

        Assert.Equal(0x0, GetOffset(in s, in s.Magic));
        Assert.Equal(0x4, GetOffset(in s, in s.Reserved));
        Assert.Equal(0x8, GetOffset(in s, in s.EntryCount));
    }

    [Fact]
    public static void KeyValueArchiveEntryHeader_Layout()
    {
        var s = new KeyValueArchiveEntryHeader();

        Assert.Equal(0xC, Unsafe.SizeOf<KeyValueArchiveEntryHeader>());

        Assert.Equal(0x0, GetOffset(in s, in s.Magic));
        Assert.Equal(0x4, GetOffset(in s, in s.KeySize));
        Assert.Equal(0x8, GetOffset(in s, in s.ValueSize));
    }
}