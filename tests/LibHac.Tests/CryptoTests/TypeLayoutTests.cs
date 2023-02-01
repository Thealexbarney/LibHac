using System.Runtime.CompilerServices;
using LibHac.Crypto;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.CryptoTests;

public class TypeLayoutTests
{
    [Fact]
    public static void RsaFullKey_Layout()
    {
        var s = new RsaFullKey();

        Assert.Equal(0x484, Unsafe.SizeOf<RsaFullKey>());

        Assert.Equal(0x000, GetOffset(in s, in s.PrivateExponent));
        Assert.Equal(0x100, GetOffset(in s, in s.Modulus));
        Assert.Equal(0x200, GetOffset(in s, in s.PublicExponent));
        Assert.Equal(0x204, GetOffset(in s, in s.Dp));
        Assert.Equal(0x284, GetOffset(in s, in s.Dq));
        Assert.Equal(0x304, GetOffset(in s, in s.InverseQ));
        Assert.Equal(0x384, GetOffset(in s, in s.P));
        Assert.Equal(0x404, GetOffset(in s, in s.Q));
    }

    [Fact]
    public static void RsaKeyPair_Layout()
    {
        var s = new RsaKeyPair();

        Assert.Equal(0x210, Unsafe.SizeOf<RsaKeyPair>());

        Assert.Equal(0x000, GetOffset(in s, in s.PrivateExponent));
        Assert.Equal(0x100, GetOffset(in s, in s.Modulus));
        Assert.Equal(0x200, GetOffset(in s, in s.PublicExponent));
        Assert.Equal(0x204, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void RsaKey_Layout()
    {
        var s = new RsaKey();

        Assert.Equal(0x104, Unsafe.SizeOf<RsaKey>());

        Assert.Equal(0x000, GetOffset(in s, in s.Modulus));
        Assert.Equal(0x100, GetOffset(in s, in s.PublicExponent));
    }

    [Fact]
    public static void AesKey_Layout()
    {
        Assert.Equal(0x10, Unsafe.SizeOf<AesKey>());
    }

    [Fact]
    public static void AesXtsKey_Layout()
    {
        Assert.Equal(0x20, Unsafe.SizeOf<AesXtsKey>());
    }

    [Fact]
    public static void AesIv_Layout()
    {
        Assert.Equal(0x10, Unsafe.SizeOf<AesIv>());
    }

    [Fact]
    public static void AesCmac_Layout()
    {
        Assert.Equal(0x10, Unsafe.SizeOf<Crypto.AesCmac>());
    }
}