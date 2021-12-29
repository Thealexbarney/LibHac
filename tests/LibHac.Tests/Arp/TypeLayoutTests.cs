using System.Runtime.CompilerServices;
using LibHac.Arp;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Arp;

public class TypeLayoutTests
{
    [Fact]
    public static void ApplicationLaunchProperty_Layout()
    {
        var s = new ApplicationLaunchProperty();

        Assert.Equal(0x10, Unsafe.SizeOf<ApplicationLaunchProperty>());

        Assert.Equal(0x0, GetOffset(in s, in s.ApplicationId));
        Assert.Equal(0x8, GetOffset(in s, in s.Version));
        Assert.Equal(0xC, GetOffset(in s, in s.StorageId));
        Assert.Equal(0xD, GetOffset(in s, in s.PatchStorageId));
        Assert.Equal(0xE, GetOffset(in s, in s.ApplicationKind));
    }
}