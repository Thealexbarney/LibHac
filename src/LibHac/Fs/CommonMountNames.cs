using LibHac.Common;

namespace LibHac.Fs
{
    internal static class CommonMountNames
    {
        public static readonly U8String GameCardMountName = new U8String("@Gc");
        public static readonly U8String SystemContentMountName = new U8String("@SystemContent");
        public static readonly U8String UserContentMountName = new U8String("@UserContent");
        public static readonly U8String SdCardContentMountName = new U8String("@SdCardContent");
        public static readonly U8String CalibrationPartitionMountName = new U8String("@CalibFile");
        public static readonly U8String SafePartitionMountName = new U8String("@Safe");
        public static readonly U8String UserPartitionMountName = new U8String("@User");
        public static readonly U8String SystemPartitionMountName = new U8String("@System");
        public static readonly U8String SdCardMountName = new U8String("@Sdcard");
        public static readonly U8String HostMountName = new U8String("@Host");
        public static readonly U8String RegisteredUpdatePartitionMountName = new U8String("@RegUpdate");
    }
}
