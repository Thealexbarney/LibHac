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

    public enum SaveDataSpaceId
    {
        System = 0,
        User = 1,
        SdSystem = 2,
        TemporaryStorage = 3,
        SdCache = 4,
        ProperSystem = 100,
        Safe = 101
    }

    public enum CustomStorageId
    {
        User = 0,
        SdCard = 1
    }
}
