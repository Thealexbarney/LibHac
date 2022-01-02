using System.Runtime.CompilerServices;
using LibHac.FsSystem;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.FsSystem;

public class TypeLayoutTests
{
    [Fact]
    public static void Hash_Layout()
    {
        var s = new Hash();

        Assert.Equal(0x20, Unsafe.SizeOf<Hash>());

        Assert.Equal(0x0, GetOffset(in s, in s.Value));
    }
}