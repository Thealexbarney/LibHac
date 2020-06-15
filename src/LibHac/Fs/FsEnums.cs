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
        Creating = 1,
        State2 = 2,
        MarkedForDeletion = 3,
        Extending = 4
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
        NeedsSecureDelete = 1 << 3
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

    [Flags]
    public enum MountHostOption
    {
        None = 0,
        PseudoCaseSensitive = 1
    }
}
