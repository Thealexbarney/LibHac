using System.Runtime.InteropServices;
using LibHac.Fat;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Explicit, Size = 0x80)]
    public struct FileSystemProxyErrorInfo
    {
        [FieldOffset(0x00)] public int RomFsRemountForDataCorruptionCount;
        [FieldOffset(0x04)] public int RomFsUnrecoverableDataCorruptionByRemountCount;
        [FieldOffset(0x08)] public FatError FatError;
        [FieldOffset(0x28)] public int RomFsRecoveredByInvalidateCacheCount;
        [FieldOffset(0x2C)] public int SaveDataIndexCount;
    }

    [StructLayout(LayoutKind.Explicit, Size = 0x10)]
    public struct StorageErrorInfo
    {
        [FieldOffset(0x00)] public int NumActivationFailures;
        [FieldOffset(0x04)] public int NumActivationErrorCorrections;
        [FieldOffset(0x08)] public int NumReadWriteFailures;
        [FieldOffset(0x0C)] public int NumReadWriteErrorCorrections;
    }
}
