using System;

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
        Invalid = 35
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
        Normal = 0,
        Secure = 1,
        Writable = 2
    }

    public enum SaveDataSpaceId : byte
    {
        System = 0,
        User = 1,
        SdSystem = 2,
        TemporaryStorage = 3,
        SdCache = 4,
        ProperSystem = 100,
        Safe = 101,
        BisAuto = 127
    }

    public enum CustomStorageId
    {
        User = 0,
        SdCard = 1
    }

    public enum FileSystemType
    {
        Code = 0,
        Data = 1,
        Logo = 2,
        ContentControl = 3,
        ContentManual = 4,
        ContentMeta = 5,
        ContentData = 6,
        ApplicationPackage = 7,
        RegisteredUpdate = 8
    }

    public enum SaveMetaType : byte
    {
        None = 0,
        Thumbnail = 1,
        ExtensionInfo = 2
    }

    public enum SaveDataState : byte
    {
        Normal = 0,
        Creating = 1,
        State2 = 2,
        MarkedForDeletion = 3,
        State4 = 4,
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
        ReadWrite = Read | Write
    }

    [Flags]
    public enum ReadOption
    {
        None = 0
    }

    [Flags]
    public enum WriteOption
    {
        None = 0,
        Flush = 1
    }

    public enum OperationId
    {
        Clear = 0,
        ClearSignature = 1,
        InvalidateCache = 2,
        QueryRange = 3
    }
}
