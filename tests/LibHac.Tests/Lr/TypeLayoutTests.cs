using System.Runtime.CompilerServices;
using LibHac.Lr;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Lr;

public class TypeLayoutTests
{
    [Fact]
    public static void IniHeader_Layout()
    {
        var s = new Path();

        Assert.Equal(0x300, Unsafe.SizeOf<Path>());

        Assert.Equal(0x0, GetOffset(in s, in s.Value));
    }
}