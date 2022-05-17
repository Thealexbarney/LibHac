using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.Tests.Fs.FileSystemClientTests;
using Xunit;

namespace LibHac.Tests.Fs.FsaTests;

public class MountUtilityTests
{
    [Theory]
    [InlineData("0123456789ABCDE", "0123456789ABCDE:/")]
    [InlineData("01234", "01234:/")]
    public void GetMountName_ValidName_ReturnsSuccess(string mountName, string path)
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.MountSdCard(mountName.ToU8Span()));
        Assert.Success(fs.GetEntryType(out _, path.ToU8Span()));
    }

    [Theory]
    [InlineData("01234", "01234")]
    [InlineData("0123456789ABCDE", "0123456789ABCDE")]
    [InlineData("01234", "0123456789ABCDEF")]
    [InlineData("01234", "0123456789ABCDEF:/")]
    public void GetMountName_InvalidName_ReturnsInvalidMountName(string mountName, string path)
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.MountSdCard(mountName.ToU8Span()));
        Assert.Result(ResultFs.InvalidMountName, fs.GetEntryType(out _, path.ToU8Span()));
    }
}