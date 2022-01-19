using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.Ncm;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests;

public class Debug
{
    [Fact]
    public void SetDebugOption_KeyIsZero_Aborts()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Throws<HorizonResultException>(() => fs.SetDebugOption(0, 1));
    }

    [Fact]
    public void SetDebugOption_NoPermissions_ReturnsPermissionDenied()
    {
        Horizon hos = HorizonFactory.CreateBasicHorizon();

        HorizonClient client =
            hos.CreateHorizonClient(new ProgramLocation(new ProgramId(1), StorageId.BuiltInSystem), 0);

        Assert.Result(ResultFs.PermissionDenied, client.Fs.SetDebugOption((DebugOptionKey)1, 0));
    }

    [Fact]
    public void SetDebugOption_DebugConfigIsFull_Aborts()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.SetDebugOption((DebugOptionKey)1, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)2, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)3, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)4, 0));

        Assert.Throws<LibHacException>(() => fs.SetDebugOption((DebugOptionKey)5, 0));
    }

    [Fact]
    public void SetDebugOption_ReplaceExistingValueWhenFull_ReturnsSuccess()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.SetDebugOption((DebugOptionKey)1, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)2, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)3, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)4, 0));

        Assert.Success(fs.SetDebugOption((DebugOptionKey)1, 10));
    }

    [Fact]
    public void SetDebugOption_AfterRemovingKeyWhenFull_ReturnsSuccess()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.SetDebugOption((DebugOptionKey)1, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)2, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)3, 0));
        Assert.Success(fs.SetDebugOption((DebugOptionKey)4, 0));

        Assert.Success(fs.UnsetDebugOption((DebugOptionKey)2));

        Assert.Success(fs.SetDebugOption((DebugOptionKey)2, 4));
    }

    [Fact]
    public void UnsetDebugOption_UnsetExistingKey_ReturnsSuccess()
    {
        const DebugOptionKey key = DebugOptionKey.SaveDataEncryption;
        const long value = 0;

        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.SetDebugOption(key, value));
        Assert.Success(fs.UnsetDebugOption(key));
    }

    [Fact]
    public void UnsetDebugOption_NoPermissions_ReturnsPermissionDenied()
    {
        Horizon hos = HorizonFactory.CreateBasicHorizon();

        HorizonClient client =
            hos.CreateHorizonClient(new ProgramLocation(new ProgramId(1), StorageId.BuiltInSystem), 0);

        Assert.Result(ResultFs.PermissionDenied, client.Fs.UnsetDebugOption((DebugOptionKey)1));
    }
}