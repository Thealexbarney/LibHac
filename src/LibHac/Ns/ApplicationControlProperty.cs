using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Ncm;

namespace LibHac.Ns
{
    [StructLayout(LayoutKind.Explicit, Size = 0x4000)]
    public struct ApplicationControlProperty
    {
        private const int TitleCount = 0x10;
        private const int IsbnSize = 0x25;
        private const int RatingAgeCount = 0x20;
        private const int DisplayVersionSize = 0x10;
        private const int ApplicationErrorCodeCategorySize = 8;
        private const int LocalCommunicationIdCount = 8;
        private const int Reserved30F3Size = 3;
        private const int BcatPassphraseSize = 0x41;
        private const int ReservedForUserAccountSaveDataOperationSize = 6;
        private const int PlayLogQueryableApplicationIdCount = 0x10;
        private const int ReceivableDataConfigurationCount = 0x10;

        [FieldOffset(0x0000)] private byte _titles;
        [FieldOffset(0x3000)] private byte _isbn;
        [FieldOffset(0x3025)] public StartupUserAccount StartupUserAccount;
        [FieldOffset(0x3026)] public byte UserAccountSwitchLock;
        [FieldOffset(0x3027)] public byte AddOnContentRegistrationType;
        [FieldOffset(0x3028)] public ApplicationAttribute ApplicationAttribute;
        [FieldOffset(0x302C)] public uint SupportedLanguages;
        [FieldOffset(0x3030)] public ParentalControlFlagValue ParentalControl;
        [FieldOffset(0x3034)] public ScreenshotValue Screenshot;
        [FieldOffset(0x3035)] public VideoCaptureValue VideoCaptureMode;
        [FieldOffset(0x3036)] public byte DataLossConfirmation;
        [FieldOffset(0x3037)] public byte PlayLogPolicy;
        [FieldOffset(0x3038)] public ulong PresenceGroupId;
        [FieldOffset(0x3040)] private sbyte _ratingAge;
        [FieldOffset(0x3060)] private byte _displayVersion;
        [FieldOffset(0x3070)] public ulong AddOnContentBaseId;
        [FieldOffset(0x3078)] public ProgramId SaveDataOwnerId;
        [FieldOffset(0x3080)] public long UserAccountSaveDataSize;
        [FieldOffset(0x3088)] public long UserAccountSaveDataJournalSize;
        [FieldOffset(0x3090)] public long DeviceSaveDataSize;
        [FieldOffset(0x3098)] public long DeviceSaveDataJournalSize;
        [FieldOffset(0x30A0)] public long BcatDeliveryCacheStorageSize;
        [FieldOffset(0x30A8)] private byte _applicationErrorCodeCategory;
        [FieldOffset(0x30B0)] private ulong _localCommunicationIds;
        [FieldOffset(0x30F0)] public LogoType LogoType;
        [FieldOffset(0x30F1)] public LogoHandling LogoHandling;
        [FieldOffset(0x30F2)] public byte RuntimeAddOnContentInstall;
        [FieldOffset(0x30F3)] public byte _reserved30F3;
        [FieldOffset(0x30F6)] public byte CrashReport;
        [FieldOffset(0x30F7)] public byte Hdcp;
        [FieldOffset(0x30F8)] public ulong SeedForPseudoDeviceId;
        [FieldOffset(0x3100)] private byte _bcatPassphrase;
        [FieldOffset(0x3141)] public byte StartupUserAccountOption;
        [FieldOffset(0x3142)] private byte _reservedForUserAccountSaveDataOperation;
        [FieldOffset(0x3148)] public long UserAccountSaveDataMaxSize;
        [FieldOffset(0x3150)] public long UserAccountSaveDataMaxJournalSize;
        [FieldOffset(0x3158)] public long DeviceSaveDataMaxSize;
        [FieldOffset(0x3160)] public long DeviceSaveDataMaxJournalSize;
        [FieldOffset(0x3168)] public long TemporaryStorageSize;
        [FieldOffset(0x3170)] public long CacheStorageSize;
        [FieldOffset(0x3178)] public long CacheStorageJournalSize;
        [FieldOffset(0x3180)] public long CacheStorageMaxSizeAndMaxJournalSize;
        [FieldOffset(0x3188)] public long CacheStorageMaxIndex;
        [FieldOffset(0x3190)] private ulong _playLogQueryableApplicationId;
        [FieldOffset(0x3210)] public PlayLogQueryCapability PlayLogQueryCapability;
        [FieldOffset(0x3211)] public byte RepairFlag;
        [FieldOffset(0x3212)] public byte ProgramIndex;
        [FieldOffset(0x3213)] public byte RequiredNetworkServiceLicenseOnLaunchFlag;
        [FieldOffset(0x3214)] public uint Reserved3214;
        [FieldOffset(0x3218)] public ApplicationControlDataConfiguration SendDataConfiguration;
        [FieldOffset(0x3230)] private ApplicationControlDataConfiguration _receivableDataConfigurations;
        [FieldOffset(0x32B0)] public ulong JitConfigurationFlag;
        [FieldOffset(0x32B8)] public long MemorySize;

        [FieldOffset(0x3000), DebuggerBrowsable(DebuggerBrowsableState.Never)] private Padding200 _padding1;
        [FieldOffset(0x3200), DebuggerBrowsable(DebuggerBrowsableState.Never)] private Padding100 _padding2;

        public Span<ApplicationControlTitle> Titles => SpanHelpers.CreateSpan(ref Unsafe.As<byte, ApplicationControlTitle>(ref _titles), TitleCount);
        public U8SpanMutable Isbn => new U8SpanMutable(SpanHelpers.CreateSpan(ref _isbn, IsbnSize));
        public Span<sbyte> RatingAge => SpanHelpers.CreateSpan(ref _ratingAge, RatingAgeCount);
        public U8SpanMutable DisplayVersion => new U8SpanMutable(SpanHelpers.CreateSpan(ref _displayVersion, DisplayVersionSize));

        public U8SpanMutable ApplicationErrorCodeCategory =>
            new U8SpanMutable(SpanHelpers.CreateSpan(ref _applicationErrorCodeCategory,
                ApplicationErrorCodeCategorySize));

        public Span<ulong> LocalCommunicationIds => SpanHelpers.CreateSpan(ref _localCommunicationIds, LocalCommunicationIdCount);
        public Span<byte> Reserved30F3 => SpanHelpers.CreateSpan(ref _reserved30F3, Reserved30F3Size);
        public U8SpanMutable BcatPassphrase => new U8SpanMutable(SpanHelpers.CreateSpan(ref _bcatPassphrase, BcatPassphraseSize));

        public Span<byte> ReservedForUserAccountSaveDataOperation =>
            SpanHelpers.CreateSpan(ref _reservedForUserAccountSaveDataOperation,
                ReservedForUserAccountSaveDataOperationSize);

        public Span<ulong> PlayLogQueryableApplicationId =>
            SpanHelpers.CreateSpan(ref _playLogQueryableApplicationId, PlayLogQueryableApplicationIdCount);

        public Span<ApplicationControlDataConfiguration> ReceivableDataConfigurations =>
            SpanHelpers.CreateSpan(ref _receivableDataConfigurations, ReceivableDataConfigurationCount);
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x300)]
    public struct ApplicationControlTitle
    {
        private const int NameLength = 0x200;
        private const int PublisherLength = 0x100;

        [FieldOffset(0x000)] private byte _name;
        [FieldOffset(0x200)] private byte _publisher;

        [FieldOffset(0x000), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Padding200 _padding0;

        [FieldOffset(0x200), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Padding100 _padding200;

        public U8SpanMutable Name => new U8SpanMutable(SpanHelpers.CreateSpan(ref _name, NameLength));
        public U8SpanMutable Publisher => new U8SpanMutable(SpanHelpers.CreateSpan(ref _publisher, PublisherLength));
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x18)]
    public struct ApplicationControlDataConfiguration
    {
        [FieldOffset(0)] public ulong Id;
        [FieldOffset(8)] private byte _key;

        [FieldOffset(8), DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Padding10 _keyPadding;

        public Span<byte> Key => SpanHelpers.CreateSpan(ref _key, 0x10);
    }

    public enum StartupUserAccount : byte
    {
        None = 0,
        Required = 1,
        RequiredWithNetworkServiceAccountAvailable = 2
    }

    public enum LogoHandling : byte
    {
        Auto = 0,
        Manual = 1
    }

    public enum LogoType : byte
    {
        LicensedByNintendo = 0,
        DistributedByNintendo = 1,
        Nintendo = 2
    }

    [Flags]
    public enum ApplicationAttribute
    {
        None = 0,
        Demo = 1
    }

    public enum PlayLogQueryCapability : byte
    {
        None = 0,
        WhiteList = 1,
        All = 2
    }

    public enum ParentalControlFlagValue
    {
        None = 0,
        FreeCommunication = 1
    }

    public enum ScreenshotValue : byte
    {
        Allow = 0,
        Deny = 1
    }

    public enum VideoCaptureValue : byte
    {
        Deny = 0,
        Allow = 1,
        Automatic = 2
    }
}
