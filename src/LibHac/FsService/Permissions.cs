using System;

namespace LibHac.FsService
{
    /// <summary>
    /// Permissions that control which storages or filesystems can be mounted or opened.
    /// </summary>
    public enum AccessPermissions
    {
        MountLogo = 0x0,
        MountContentMeta = 0x1,
        MountContentControl = 0x2,
        MountContentManual = 0x3,
        MountContentData = 0x4,
        MountApplicationPackage = 0x5,
        MountSaveDataStorage = 0x6,
        MountContentStorage = 0x7,
        MountImageAndVideoStorage = 0x8,
        MountCloudBackupWorkStorage = 0x9,
        MountCustomStorage = 0xA,
        MountBisCalibrationFile = 0xB,
        MountBisSafeMode = 0xC,
        MountBisUser = 0xD,
        MountBisSystem = 0xE,
        MountBisSystemProperEncryption = 0xF,
        MountBisSystemProperPartition = 0x10,
        MountSdCard = 0x11,
        MountGameCard = 0x12,
        MountDeviceSaveData = 0x13,
        MountSystemSaveData = 0x14,
        MountOthersSaveData = 0x15,
        MountOthersSystemSaveData = 0x16,
        OpenBisPartitionBootPartition1Root = 0x17,
        OpenBisPartitionBootPartition2Root = 0x18,
        OpenBisPartitionUserDataRoot = 0x19,
        OpenBisPartitionBootConfigAndPackage2Part1 = 0x1A,
        OpenBisPartitionBootConfigAndPackage2Part2 = 0x1B,
        OpenBisPartitionBootConfigAndPackage2Part3 = 0x1C,
        OpenBisPartitionBootConfigAndPackage2Part4 = 0x1D,
        OpenBisPartitionBootConfigAndPackage2Part5 = 0x1E,
        OpenBisPartitionBootConfigAndPackage2Part6 = 0x1F,
        OpenBisPartitionCalibrationBinary = 0x20,
        OpenBisPartitionCalibrationFile = 0x21,
        OpenBisPartitionSafeMode = 0x22,
        OpenBisPartitionUser = 0x23,
        OpenBisPartitionSystem = 0x24,
        OpenBisPartitionSystemProperEncryption = 0x25,
        OpenBisPartitionSystemProperPartition = 0x26,
        OpenSdCardStorage = 0x27,
        OpenGameCardStorage = 0x28,
        MountSystemDataPrivate = 0x29,
        MountHost = 0x2A,
        MountRegisteredUpdatePartition = 0x2B,
        MountSaveDataInternalStorage = 0x2C,
        NotMountCustomStorage = 0x2D
    }

    /// <summary>
    /// Permissions that control which actions can be performed.
    /// </summary>
    public enum ActionPermissions
    {
        // Todo
    }

    public static class PermissionUtils
    {
        public static ulong GetPermissionMask(AccessPermissions id)
        {
            switch (id)
            {
                case AccessPermissions.MountLogo:
                    return 0x8000000000000801;
                case AccessPermissions.MountContentMeta:
                    return 0x8000000000000801;
                case AccessPermissions.MountContentControl:
                    return 0x8000000000000801;
                case AccessPermissions.MountContentManual:
                    return 0x8000000000000801;
                case AccessPermissions.MountContentData:
                    return 0x8000000000000801;
                case AccessPermissions.MountApplicationPackage:
                    return 0x8000000000000801;
                case AccessPermissions.MountSaveDataStorage:
                    return 0x8000000000000000;
                case AccessPermissions.MountContentStorage:
                    return 0x8000000000000800;
                case AccessPermissions.MountImageAndVideoStorage:
                    return 0x8000000000001000;
                case AccessPermissions.MountCloudBackupWorkStorage:
                    return 0x8000000200000000;
                case AccessPermissions.MountCustomStorage:
                    return 0x8000000000000000;
                case AccessPermissions.MountBisCalibrationFile:
                    return 0x8000000000000084;
                case AccessPermissions.MountBisSafeMode:
                    return 0x8000000000000080;
                case AccessPermissions.MountBisUser:
                    return 0x8000000000008080;
                case AccessPermissions.MountBisSystem:
                    return 0x8000000000008080;
                case AccessPermissions.MountBisSystemProperEncryption:
                    return 0x8000000000000080;
                case AccessPermissions.MountBisSystemProperPartition:
                    return 0x8000000000000080;
                case AccessPermissions.MountSdCard:
                    return 0xC000000000200000;
                case AccessPermissions.MountGameCard:
                    return 0x8000000000000010;
                case AccessPermissions.MountDeviceSaveData:
                    return 0x8000000000040020;
                case AccessPermissions.MountSystemSaveData:
                    return 0x8000000000000028;
                case AccessPermissions.MountOthersSaveData:
                    return 0x8000000000000020;
                case AccessPermissions.MountOthersSystemSaveData:
                    return 0x8000000000000020;
                case AccessPermissions.OpenBisPartitionBootPartition1Root:
                    return 0x8000000000010082;
                case AccessPermissions.OpenBisPartitionBootPartition2Root:
                    return 0x8000000000010080;
                case AccessPermissions.OpenBisPartitionUserDataRoot:
                    return 0x8000000000000080;
                case AccessPermissions.OpenBisPartitionBootConfigAndPackage2Part1:
                    return 0x8000000000010080;
                case AccessPermissions.OpenBisPartitionBootConfigAndPackage2Part2:
                    return 0x8000000000010080;
                case AccessPermissions.OpenBisPartitionBootConfigAndPackage2Part3:
                    return 0x8000000000010080;
                case AccessPermissions.OpenBisPartitionBootConfigAndPackage2Part4:
                    return 0x8000000000010080;
                case AccessPermissions.OpenBisPartitionBootConfigAndPackage2Part5:
                    return 0x8000000000010080;
                case AccessPermissions.OpenBisPartitionBootConfigAndPackage2Part6:
                    return 0x8000000000010080;
                case AccessPermissions.OpenBisPartitionCalibrationBinary:
                    return 0x8000000000000084;
                case AccessPermissions.OpenBisPartitionCalibrationFile:
                    return 0x8000000000000084;
                case AccessPermissions.OpenBisPartitionSafeMode:
                    return 0x8000000000000080;
                case AccessPermissions.OpenBisPartitionUser:
                    return 0x8000000000000080;
                case AccessPermissions.OpenBisPartitionSystem:
                    return 0x8000000000000080;
                case AccessPermissions.OpenBisPartitionSystemProperEncryption:
                    return 0x8000000000000080;
                case AccessPermissions.OpenBisPartitionSystemProperPartition:
                    return 0x8000000000000080;
                case AccessPermissions.OpenSdCardStorage:
                    return 0xC000000000200000;
                case AccessPermissions.OpenGameCardStorage:
                    return 0x8000000000000100;
                case AccessPermissions.MountSystemDataPrivate:
                    return 0x8000000000100008;
                case AccessPermissions.MountHost:
                    return 0xC000000000400000;
                case AccessPermissions.MountRegisteredUpdatePartition:
                    return 0x8000000000010000;
                case AccessPermissions.MountSaveDataInternalStorage:
                    return 0x8000000000000000;
                case AccessPermissions.NotMountCustomStorage:
                    return 0x0;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, null);
            }
        }
    }
}
