using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests;

public class Bis
{
    [Fact]
    public void MountBis_MountCalibrationPartition_OpensCorrectDirectory()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(out IFileSystem rootFs);

        Assert.Success(fs.MountBis("calib"u8, BisPartitionId.CalibrationFile));

        // Create a file in the opened file system
        Assert.Success(fs.CreateFile("calib:/file"u8, 0));

        // Make sure the file exists on the root file system
        Assert.Success(rootFs.GetEntryType(out DirectoryEntryType type, "/bis/cal/file"u8));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void MountBis_MountSafePartition_OpensCorrectDirectory()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(out IFileSystem rootFs);

        Assert.Success(fs.MountBis("safe"u8, BisPartitionId.SafeMode));

        // Create a file in the opened file system
        Assert.Success(fs.CreateFile("safe:/file"u8, 0));

        // Make sure the file exists on the root file system
        Assert.Success(rootFs.GetEntryType(out DirectoryEntryType type, "/bis/safe/file"u8));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void MountBis_MountSystemPartition_OpensCorrectDirectory()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(out IFileSystem rootFs);

        Assert.Success(fs.MountBis("system"u8, BisPartitionId.System));

        // Create a file in the opened file system
        Assert.Success(fs.CreateFile("system:/file"u8, 0));

        // Make sure the file exists on the root file system
        Assert.Success(rootFs.GetEntryType(out DirectoryEntryType type, "/bis/system/file"u8));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void MountBis_MountUserPartition_OpensCorrectDirectory()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(out IFileSystem rootFs);

        Assert.Success(fs.MountBis("user"u8, BisPartitionId.User));

        // Create a file in the opened file system
        Assert.Success(fs.CreateFile("user:/file"u8, 0));

        // Make sure the file exists on the root file system
        Assert.Success(rootFs.GetEntryType(out DirectoryEntryType type, "/bis/user/file"u8));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void MountBis_WithRootPath_IgnoresRootPath()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(out IFileSystem rootFs);

        Assert.Success(fs.MountBis(BisPartitionId.User, "/sub"u8));

        // Create a file in the opened file system
        Assert.Success(fs.CreateFile("@User:/file"u8, 0));

        // Make sure the file wasn't created in the sub path
        Assert.Result(ResultFs.PathNotFound, rootFs.GetEntryType(out _, "/bis/user/sub/file"u8));

        // Make sure the file was created in the main path
        Assert.Success(rootFs.GetEntryType(out DirectoryEntryType type, "/bis/user/file"u8));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void MountBis_InvalidPartition_ReturnsInvalidArgument()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(out IFileSystem _);

        Assert.Result(ResultFs.InvalidArgument, fs.MountBis("boot1"u8, BisPartitionId.BootPartition1Root));
    }
}