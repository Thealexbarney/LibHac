using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests;

public class DeviceSaveData
{
    [Fact]
    public static void MountDeviceSaveData_SaveDoesNotExist_ReturnsTargetNotFound()
    {
        var applicationId = new Ncm.ApplicationId(1234);
        HorizonServerSet hos = FileSystemServerFactory.CreateHorizon(applicationId, fsAcBits: AccessControlBits.Bits.FullPermission);

        Assert.Result(ResultFs.TargetNotFound, hos.Client.Fs.MountDeviceSaveData("device".ToU8Span(), applicationId));
        Assert.Result(ResultFs.TargetNotFound, hos.Client.Fs.MountDeviceSaveData("device2".ToU8Span()));
    }

    [Fact]
    public static void MountDeviceSaveData_OwnDeviceSaveExists_ReturnsSuccess()
    {
        var applicationId = new Ncm.ApplicationId(1234);
        HorizonServerSet hos = FileSystemServerFactory.CreateHorizon(applicationId, fsAcBits: AccessControlBits.Bits.FullPermission);

        Assert.Success(hos.Client.Fs.CreateDeviceSaveData(applicationId, applicationId.Value, 0, 0, SaveDataFlags.None));
        Assert.Success(hos.Client.Fs.MountDeviceSaveData("device".ToU8Span()));
        Assert.Success(hos.Client.Fs.MountDeviceSaveData("device2".ToU8Span(), applicationId));
    }

    [Fact]
    public static void MountDeviceSaveData_OtherDeviceSaveExists_ReturnsSuccess()
    {
        var ownApplicationId = new Ncm.ApplicationId(1234);
        var otherApplicationId = new Ncm.ApplicationId(12345);
        HorizonServerSet hos = FileSystemServerFactory.CreateHorizon(ownApplicationId, fsAcBits: AccessControlBits.Bits.FullPermission);

        Assert.Success(hos.Client.Fs.CreateDeviceSaveData(otherApplicationId, otherApplicationId.Value, 0, 0, SaveDataFlags.None));
        Assert.Success(hos.Client.Fs.MountDeviceSaveData("device".ToU8Span(), otherApplicationId));

        // Try to open missing own device save data
        Assert.Result(ResultFs.TargetNotFound, hos.Client.Fs.MountDeviceSaveData("device2".ToU8Span()));
    }

    [Fact]
    public static void IsDeviceSaveDataExisting_ReturnsCorrectState()
    {
        var applicationId = new ApplicationId(1234);
        var ncmApplicationId = new Ncm.ApplicationId(applicationId.Value);
        HorizonServerSet hos = FileSystemServerFactory.CreateHorizon(ncmApplicationId);
        FileSystemClient fs = hos.InitialProcessClient.Fs;

        // Should return false when there aren't any saves.
        Assert.False(fs.IsDeviceSaveDataExisting(applicationId));

        // Should return true after creating the save.
        Assert.Success(fs.CreateDeviceSaveData(ncmApplicationId, applicationId.Value, 0, 0, SaveDataFlags.None));
        Assert.True(fs.IsDeviceSaveDataExisting(applicationId));

        // Should return false after deleting the save.
        Assert.Success(fs.DeleteDeviceSaveData(applicationId));
        Assert.False(fs.IsDeviceSaveDataExisting(applicationId));
    }
}