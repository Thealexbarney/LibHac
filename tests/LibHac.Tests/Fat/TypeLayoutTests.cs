using System.Runtime.CompilerServices;
using LibHac.Fat;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Fat;

public class TypeLayoutTests
{
    [Fact]
    public static void FatError_Layout()
    {
        var s = new FatError();

        Assert.Equal(0x20, Unsafe.SizeOf<FatError>());

        Assert.Equal(0x00, GetOffset(in s, in s.Error));
        Assert.Equal(0x04, GetOffset(in s, in s.ExtraError));
        Assert.Equal(0x08, GetOffset(in s, in s.DriveId));
        Assert.Equal(0x0C, GetOffset(in s, in s.ErrorName));
        Assert.Equal(0x1C, GetOffset(in s, in s.Reserved));
    }
}