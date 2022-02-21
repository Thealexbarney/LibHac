using System.Runtime.CompilerServices;
using LibHac.Ns;
using Xunit;
using static LibHac.Ns.ApplicationControlProperty;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Ns;

public class TypeLayoutTests
{
    [Fact]
    public static void ApplicationTitle_Layout()
    {
        var s = new ApplicationTitle();

        Assert.Equal(0x300, Unsafe.SizeOf<ApplicationTitle>());

        Assert.Equal(0x000, GetOffset(in s, in s.Name));
        Assert.Equal(0x200, GetOffset(in s, in s.Publisher));

        Assert.Equal(0x000, GetOffset(in s, in s.NameString.Value[0]));
        Assert.Equal(0x200, GetOffset(in s, in s.PublisherString.Value[0]));
    }

    [Fact]
    public static void ApplicationNeighborDetectionGroupConfiguration_Layout()
    {
        var s = new ApplicationNeighborDetectionGroupConfiguration();

        Assert.Equal(0x18, Unsafe.SizeOf<ApplicationNeighborDetectionGroupConfiguration>());

        Assert.Equal(0x0, GetOffset(in s, in s.GroupId));
        Assert.Equal(0x8, GetOffset(in s, in s.Key));
    }

    [Fact]
    public static void ApplicationNeighborDetectionClientConfiguration_Layout()
    {
        var s = new ApplicationNeighborDetectionClientConfiguration();

        Assert.Equal(0x198, Unsafe.SizeOf<ApplicationNeighborDetectionClientConfiguration>());

        Assert.Equal(0x00, GetOffset(in s, in s.SendGroupConfiguration));
        Assert.Equal(0x18, GetOffset(in s, in s.ReceivableGroupConfigurations));
    }

    [Fact]
    public static void ApplicationJitConfiguration_Layout()
    {
        var s = new ApplicationJitConfiguration();

        Assert.Equal(0x10, Unsafe.SizeOf<ApplicationJitConfiguration>());

        Assert.Equal(0x0, GetOffset(in s, in s.Flags));
        Assert.Equal(0x8, GetOffset(in s, in s.MemorySize));
    }

    [Fact]
    public static void ApplicationControlProperty_Layout()
    {
        var s = new ApplicationControlProperty();

        Assert.Equal(0x4000, Unsafe.SizeOf<ApplicationControlProperty>());

        Assert.Equal(0x0000, GetOffset(in s, in s.Title));
        Assert.Equal(0x3000, GetOffset(in s, in s.Isbn));
        Assert.Equal(0x3025, GetOffset(in s, in s.StartupUserAccount));
        Assert.Equal(0x3026, GetOffset(in s, in s.UserAccountSwitchLock));
        Assert.Equal(0x3027, GetOffset(in s, in s.AddOnContentRegistrationType));
        Assert.Equal(0x3028, GetOffset(in s, in s.AttributeFlag));
        Assert.Equal(0x302C, GetOffset(in s, in s.SupportedLanguageFlag));
        Assert.Equal(0x3030, GetOffset(in s, in s.ParentalControlFlag));
        Assert.Equal(0x3034, GetOffset(in s, in s.Screenshot));
        Assert.Equal(0x3035, GetOffset(in s, in s.VideoCapture));
        Assert.Equal(0x3036, GetOffset(in s, in s.DataLossConfirmation));
        Assert.Equal(0x3037, GetOffset(in s, in s.PlayLogPolicy));
        Assert.Equal(0x3038, GetOffset(in s, in s.PresenceGroupId));
        Assert.Equal(0x3040, GetOffset(in s, in s.RatingAge));
        Assert.Equal(0x3060, GetOffset(in s, in s.DisplayVersion));
        Assert.Equal(0x3070, GetOffset(in s, in s.AddOnContentBaseId));
        Assert.Equal(0x3078, GetOffset(in s, in s.SaveDataOwnerId));
        Assert.Equal(0x3080, GetOffset(in s, in s.UserAccountSaveDataSize));
        Assert.Equal(0x3088, GetOffset(in s, in s.UserAccountSaveDataJournalSize));
        Assert.Equal(0x3090, GetOffset(in s, in s.DeviceSaveDataSize));
        Assert.Equal(0x3098, GetOffset(in s, in s.DeviceSaveDataJournalSize));
        Assert.Equal(0x30A0, GetOffset(in s, in s.BcatDeliveryCacheStorageSize));
        Assert.Equal(0x30A8, GetOffset(in s, in s.ApplicationErrorCodeCategory));
        Assert.Equal(0x30B0, GetOffset(in s, in s.LocalCommunicationId));
        Assert.Equal(0x30F0, GetOffset(in s, in s.LogoType));
        Assert.Equal(0x30F1, GetOffset(in s, in s.LogoHandling));
        Assert.Equal(0x30F2, GetOffset(in s, in s.RuntimeAddOnContentInstall));
        Assert.Equal(0x30F3, GetOffset(in s, in s.RuntimeParameterDelivery));
        Assert.Equal(0x30F4, GetOffset(in s, in s.Reserved30F4));
        Assert.Equal(0x30F6, GetOffset(in s, in s.CrashReport));
        Assert.Equal(0x30F7, GetOffset(in s, in s.Hdcp));
        Assert.Equal(0x30F8, GetOffset(in s, in s.SeedForPseudoDeviceId));
        Assert.Equal(0x3100, GetOffset(in s, in s.BcatPassphrase));
        Assert.Equal(0x3141, GetOffset(in s, in s.StartupUserAccountOption));
        Assert.Equal(0x3142, GetOffset(in s, in s.ReservedForUserAccountSaveDataOperation));
        Assert.Equal(0x3148, GetOffset(in s, in s.UserAccountSaveDataSizeMax));
        Assert.Equal(0x3150, GetOffset(in s, in s.UserAccountSaveDataJournalSizeMax));
        Assert.Equal(0x3158, GetOffset(in s, in s.DeviceSaveDataSizeMax));
        Assert.Equal(0x3160, GetOffset(in s, in s.DeviceSaveDataJournalSizeMax));
        Assert.Equal(0x3168, GetOffset(in s, in s.TemporaryStorageSize));
        Assert.Equal(0x3170, GetOffset(in s, in s.CacheStorageSize));
        Assert.Equal(0x3178, GetOffset(in s, in s.CacheStorageJournalSize));
        Assert.Equal(0x3180, GetOffset(in s, in s.CacheStorageDataAndJournalSizeMax));
        Assert.Equal(0x3188, GetOffset(in s, in s.CacheStorageIndexMax));
        Assert.Equal(0x318A, GetOffset(in s, in s.Reserved318A));
        Assert.Equal(0x318B, GetOffset(in s, in s.RuntimeUpgrade));
        Assert.Equal(0x318C, GetOffset(in s, in s.SupportingLimitedLicenses));
        Assert.Equal(0x3190, GetOffset(in s, in s.PlayLogQueryableApplicationId));
        Assert.Equal(0x3210, GetOffset(in s, in s.PlayLogQueryCapability));
        Assert.Equal(0x3211, GetOffset(in s, in s.RepairFlag));
        Assert.Equal(0x3212, GetOffset(in s, in s.ProgramIndex));
        Assert.Equal(0x3213, GetOffset(in s, in s.RequiredNetworkServiceLicenseOnLaunchFlag));
        Assert.Equal(0x3214, GetOffset(in s, in s.Reserved3214));
        Assert.Equal(0x3218, GetOffset(in s, in s.NeighborDetectionClientConfiguration));
        Assert.Equal(0x33B0, GetOffset(in s, in s.JitConfiguration));
        Assert.Equal(0x33C0, GetOffset(in s, in s.RequiredAddOnContentsSetBinaryDescriptors));
        Assert.Equal(0x3400, GetOffset(in s, in s.PlayReportPermission));
        Assert.Equal(0x3401, GetOffset(in s, in s.CrashScreenshotForProd));
        Assert.Equal(0x3402, GetOffset(in s, in s.CrashScreenshotForDev));
        Assert.Equal(0x3403, GetOffset(in s, in s.ContentsAvailabilityTransitionPolicy));
        Assert.Equal(0x3404, GetOffset(in s, in s.Reserved3404));
        Assert.Equal(0x3408, GetOffset(in s, in s.AccessibleLaunchRequiredVersion));
        Assert.Equal(0x3448, GetOffset(in s, in s.Reserved3448));
    }
}