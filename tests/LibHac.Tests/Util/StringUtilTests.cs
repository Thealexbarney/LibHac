using System;
using System.Text;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Util;

public class StringUtilTests
{
    [Theory]
    [InlineData("abcdef", 3, 1, "", 6)]
    [InlineData("abcdef", 3, 3, "ab", 6)]
    [InlineData("abcdef", 3, 6, "ab", 6)]
    [InlineData("abcdef", 6, 6, "abcde", 6)]
    [InlineData("abcdef", 7, 6, "abcde", 6)]
    [InlineData("abcdef", 7, 7, "abcdef", 6)]
    [InlineData("abcdef", 10, 10, "abcdef", 6)]
    public void Strlcpy_TestKnownInputs(string source, int destBufferSize, int size, string expected, int expectedReturnValue)
    {
        const byte paddingValue = (byte)'X';

        byte[] src = Encoding.ASCII.GetBytes(source);
        byte[] expectedBytes = Encoding.ASCII.GetBytes(expected);
        byte[] dest = new byte[destBufferSize];
        dest.AsSpan().Fill(paddingValue);

        int returnValue = StringUtils.Strlcpy(dest, src, size);

        Assert.Equal(expectedReturnValue, returnValue);

        for (int i = 0; i < expectedBytes.Length; i++)
        {
            Assert.Equal(expectedBytes[i], dest[i]);
        }

        Assert.Equal(0, dest[expectedBytes.Length]);

        for (int i = expectedBytes.Length + 1; i < dest.Length; i++)
        {
            Assert.Equal(paddingValue, dest[i]);
        }
    }
}