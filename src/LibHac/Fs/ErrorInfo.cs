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
    public FatReportInfo BisSystemFatReportInfo;
    public FatReportInfo BisUserFatReport;
    public FatReportInfo SdCardFatReport;
    public Array68<byte> Reserved;
}

public struct StorageErrorInfo
{
    public int NumActivationFailures;
    public int NumActivationErrorCorrections;
    public int NumReadWriteFailures;
    public int NumReadWriteErrorCorrections;
}