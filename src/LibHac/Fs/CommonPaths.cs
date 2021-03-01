using System;
using LibHac.Common;

namespace LibHac.Fs
{
    // Todo: Migrate to LibHac.Fs.Impl.CommonMountNames and remove
    internal static class CommonPaths
    {
        public const char ReservedMountNamePrefixCharacter = '@';

        public static readonly U8String GameCardFileSystemMountName = new U8String("@Gc");
        public static readonly U8String ContentStorageSystemMountName = new U8String("@SystemContent");
        public static readonly U8String ContentStorageUserMountName = new U8String("@UserContent");
        public static readonly U8String ContentStorageSdCardMountName = new U8String("@SdCardContent");
        public static readonly U8String BisCalibrationFilePartitionMountName = new U8String("@CalibFile");
        public static readonly U8String BisSafeModePartitionMountName = new U8String("@Safe");
        public static readonly U8String BisUserPartitionMountName = new U8String("@User");
        public static readonly U8String BisSystemPartitionMountName = new U8String("@System");
        public static readonly U8String SdCardFileSystemMountName = new U8String("@Sdcard");
        public static readonly U8String HostRootFileSystemMountName = new U8String("@Host");
        public static readonly U8String RegisteredUpdatePartitionMountName = new U8String("@RegUpdate");

        public const char GameCardFileSystemMountNameUpdateSuffix = 'U';
        public const char GameCardFileSystemMountNameNormalSuffix = 'N';
        public const char GameCardFileSystemMountNameSecureSuffix = 'S';

        public static ReadOnlySpan<byte> SdCardNintendoRootDirectoryName => // Nintendo
            new[]
            {
                (byte) 'N', (byte) 'i', (byte) 'n', (byte) 't', (byte) 'e', (byte) 'n', (byte) 'd', (byte) 'o'
            };
    }
}
