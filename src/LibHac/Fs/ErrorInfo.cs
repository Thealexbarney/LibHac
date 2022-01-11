using LibHac.Common.FixedArrays;
using LibHac.Fat;

namespace LibHac.Fs;

public struct FileSystemProxyErrorInfo
{
    public int RemountForDataCorruptionCount;
    public int UnrecoverableDataCorruptionByRemountCount;
    public FatError FatFsError;
    public int RecoveredByInvalidateCacheCount;
    public int SaveDataIndexCount;
    public Array80<byte> Reserved;
}

public struct StorageErrorInfo
{
    public int NumActivationFailures;
    public int NumActivationErrorCorrections;
    public int NumReadWriteFailures;
    public int NumReadWriteErrorCorrections;
}