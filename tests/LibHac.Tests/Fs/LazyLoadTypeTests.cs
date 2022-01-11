// ReSharper disable InconsistentNaming

using System.Runtime.CompilerServices;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs;

public class LazyLoadTypeTests
{
    [Fact]
    public static void UnpreparedRangeInfoSizeIs0x40()
    {
        Assert.Equal(0x40, Unsafe.SizeOf<UnpreparedRangeInfo>());
    }

    [Fact]
    public static void UnpreparedFileInformationSizeIs0x301()
    {
        Assert.Equal(0x301, Unsafe.SizeOf<UnpreparedFileInformation>());
    }

    [Fact]
    public static void LazyLoadArgumentsSizeIs0x40()
    {
        Assert.Equal(0x40, Unsafe.SizeOf<LazyLoadArguments>());
    }
}