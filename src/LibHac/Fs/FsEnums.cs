using System;
using LibHac.Fs.Fsa;

namespace LibHac.Fs;

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
    SignedSystemPartitionOnSafeMode = 34,
    DeviceTreeBlob = 35,
    System0 = 36
}

public enum ContentStorageId
{
    System = 0,
    User = 1,
    SdCard = 2,
    System0 = 3
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
    FillZero = 0,
    DestroySignature = 1,
    InvalidateCache = 2,
    QueryRange = 3,
    QueryUnpreparedRange = 4,
    QueryLazyLoadCompletionRate = 5,
    SetLazyLoadPriority = 6,
    ReadyLazyLoadFile = 10001
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
    Unknown = 8
}

public enum SdmmcBusWidth
{
    Unknown = 0,
    Width1Bit = 1,
    Width4Bit = 2,
    Width8Bit = 3,
}

public enum SdmmcSpeedMode
{
    Unknown = 0,
    MmcIdentification = 1,
    MmcLegacySpeed = 2,
    MmcHighSpeed = 3,
    MmcHs200 = 4,
    MmcHs400 = 5,
    SdCardIdentification = 6,
    SdCardDefaultSpeed = 7,
    SdCardHighSpeed = 8,
    SdCardSdr12 = 9,
    SdCardSdr25 = 10,
    SdCardSdr50 = 11,
    SdCardSdr104 = 12,
    SdCardDdr50 = 13,
    GcAsicFpgaSpeed = 14,
    GcAsicSpeed = 15
}

public enum SdmmcPort
{
    Mmc = 0,
    SdCard = 1,
    GcAsic = 2
}