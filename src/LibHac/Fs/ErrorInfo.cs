using LibHac.Common.FixedArrays;
using LibHac.Fat;

namespace LibHac.Fs;

public struct FileSystemProxyErrorInfo
{
    public uint RemountForDataCorruptionCount;
    public uint UnrecoverableDataCorruptionByRemountCount;
    public FatError FatFsError;
    public uint RecoveredByInvalidateCacheCount;
    public int SaveDataIndexCount;
    public FatReportInfo1 BisSystemFatReportInfo1;
    public FatReportInfo1 BisUserFatReport1;
    public FatReportInfo1 SdCardFatReport1;
    public FatReportInfo2 BisSystemFatReportInfo2;
    public FatReportInfo2 BisUserFatReport2;
    public FatReportInfo2 SdCardFatReport2;
    public uint DeepRetryStartCount;
    public uint UnrecoverableByGameCardAccessFailedCount;
    public FatSafeInfo BisSystemFatSafeInfo;
    public FatSafeInfo BisUserFatSafeInfo;
    public Array24<byte> Reserved;
}

public struct StorageErrorInfo
{
    public uint NumActivationFailures;
    public uint NumActivationErrorCorrections;
    public uint NumReadWriteFailures;
    public uint NumReadWriteErrorCorrections;
}