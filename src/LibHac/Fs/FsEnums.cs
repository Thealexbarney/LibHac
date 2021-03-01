using System;
using LibHac.Fs.Fsa;

namespace LibHac.Fs
{
    public enum BisPartitionId
    {
        BootPartition1Root = 0,
        BootPartition2Root = 10,
        UserDataRoot = 20,
        BootConfigAndPackage2Part1 = 21,
        BootConfigAndPackage2Part2 = 22,
        BootConfigAndPackage2Part3 = 23,
        BootConfigAndPackage2Part4 = 24,
        BootConfigAndPackage2Part5 = 25,
        BootConfigAndPackage2Part6 = 26,
        CalibrationBinary = 27,
        CalibrationFile = 28,
        SafeMode = 29,
        User = 30,
        System = 31,
        SystemProperEncryption = 32,
        SystemProperPartition = 33,
        SignedSystemPartitionOnSafeMode = 34
    }

    public enum ContentStorageId
    {
        System = 0,
        User = 1,
        SdCard = 2
    }

    public enum GameCardPartition
    {
        Update = 0,
        Normal = 1,
        Secure = 2,
        Logo = 3
    }

    public enum GameCardPartitionRaw
    {
        NormalReadOnly = 0,
        SecureReadOnly = 1,
        RootWriteOnly = 2
    }

    public enum SaveDataSpaceId : byte
    {
        System = 0,
        User = 1,
        SdSystem = 2,
        Temporary = 3,
        SdCache = 4,
        ProperSystem = 100,
        SafeMode = 101,
        BisAuto = 127
    }

    public enum CustomStorageId
    {
        System = 0,
        SdCard = 1
    }

    public enum ContentType
    {
        Meta = 0,
        Control = 1,
        Manual = 2,
        Logo = 3,
        Data = 4
    }

    public enum FileSystemProxyType
    {
        Code = 0,
        Rom = 1,
        Logo = 2,
        Control = 3,
        Manual = 4,
        Meta = 5,
        Data = 6,
        Package = 7,
        RegisteredUpdate = 8
    }

    public enum SaveDataMetaType : byte
    {
        None = 0,
        Thumbnail = 1,
        ExtensionContext = 2
    }

    public enum SaveDataState : byte
    {
        Normal = 0,
        Processing = 1,
        State2 = 2,
        MarkedForDeletion = 3,
        Extending = 4,
        ImportSuspended = 5
    }

    public enum ImageDirectoryId
    {
        Nand = 0,
        SdCard = 1
    }

    public enum CloudBackupWorkStorageId
    {
        Nand = 0,
        SdCard = 1
    }

    public enum BaseFileSystemId
    {
        ImageDirectoryNand = 0,
        ImageDirectorySdCard = 1,
        TemporaryDirectory = 2
    }

    /// <summary>
    /// Specifies which operations are available on an <see cref="IFile"/>.
    /// </summary>
    [Flags]
    public enum OpenMode
    {
        Read = 1,
        Write = 2,
        AllowAppend = 4,
        ReadWrite = Read | Write,
        All = Read | Write | AllowAppend
    }

    [Flags]
    public enum ReadOptionFlag
    {
        None = 0
    }

    public enum OperationId
    {
        Clear = 0,
        ClearSignature = 1,
        InvalidateCache = 2,
        QueryRange = 3
    }

    public enum SaveDataType : byte
    {
        System = 0,
        Account = 1,
        Bcat = 2,
        Device = 3,
        Temporary = 4,
        Cache = 5,
        SystemBcat = 6
    }

    public enum SaveDataRank : byte
    {
        Primary = 0,
        Secondary = 1
    }

    [Flags]
    public enum SaveDataFlags
    {
        None = 0,
        KeepAfterResettingSystemSaveData = 1 << 0,
        KeepAfterRefurbishment = 1 << 1,
        KeepAfterResettingSystemSaveDataWithoutUserSaveData = 1 << 2,
        NeedsSecureDelete = 1 << 3,
        Restore = 1 << 4
    }

    [Flags]
    public enum CommitOptionFlag
    {
        None = 0,
        ClearRestoreFlag = 1,
        SetRestoreFlag = 2
    }

    public enum SdmmcPort
    {
        Mmc = 0,
        SdCard = 1,
        GcAsic = 2
    }

    public enum CacheStorageTargetMedia
    {
        None = 0,
        Nand = 1,
        SdCard = 2
    }

    public enum SimulatingDeviceDetectionMode
    {
        NoSimulation = 0,
        DeviceAttached = 1,
        DeviceRemoved = 2
    }

    public enum SimulatingDeviceAccessFailureEventType
    {
        None = 0,
        AccessTimeoutFailure = 1,
        AccessFailure = 2,
        DataCorruption = 3
    }

    [Flags]
    public enum SimulatingDeviceTargetOperation
    {
        None = 0,
        Read = 1 << 0,
        Write = 1 << 1
    }

    public enum FsStackUsageThreadType
    {
        MainThread = 0,
        IpcWorker = 1,
        PipelineWorker = 2
    }

    public enum MmcPartition
    {
        UserData = 0,
        BootPartition1 = 1,
        BootPartition2 = 2
    }

    public enum MmcSpeedMode
    {
        Identification = 0,
        LegacySpeed = 1,
        HighSpeed = 2,
        Hs200 = 3,
        Hs400 = 4,
        Unknown = 5
    }

    public enum SdCardSpeedMode
    {
        Identification = 0,
        DefaultSpeed = 1,
        HighSpeed = 2,
        Sdr12 = 3,
        Sdr25 = 4,
        Sdr50 = 5,
        Sdr104 = 6,
        Ddr50 = 7,
        Unknown = 8,
    }
}
