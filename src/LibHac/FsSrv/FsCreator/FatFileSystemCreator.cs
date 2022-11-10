using System;
using LibHac.Common;
using LibHac.Fat;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Os;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Handles creating FAT file systems.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0 (FS 14.1.0)</remarks>
public class FatFileSystemCreator : IFatFileSystemCreator
{
    // ReSharper disable once NotAccessedField.Local
    private MemoryResource _allocator;
    private FatError _fatFsError;
    private SdkMutexType _fatErrorMutex;
    private FatReport _bisSystemReport;
    private FatReport _bisUserReport;
    private FatReport _sdCardReport;
    private SdkMutexType _fatReportMutex;

    public FatFileSystemCreator(MemoryResource allocator)
    {
        _fatErrorMutex = new SdkMutexType();
        _fatReportMutex = new SdkMutexType();
        _allocator = allocator;

        // Missing: Call nn::fat::SetMemoryResource
    }

    public Result Create(ref SharedRef<IFileSystem> outFileSystem, ref SharedRef<IStorage> baseStorage,
        FatAttribute attribute, int driveId, Result invalidFatFormatResult, Result usableSpaceNotEnoughResult)
    {
        throw new NotImplementedException();
    }

    public Result Format(ref SharedRef<IStorage> partitionStorage, FatAttribute attribute, FatFormatParam formatParam,
        int driveId, Result invalidFatFormatResult, Result usableSpaceNotEnoughResult)
    {
        throw new NotImplementedException();
    }

    public void GetAndClearFatFsError(out FatError outFatError)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _fatErrorMutex);

        outFatError = _fatFsError;
        _fatFsError = default;
    }

    public void GetAndClearFatReportInfo(out FatReportInfo outBisSystemFatReportInfo,
        out FatReportInfo outBisUserFatReportInfo, out FatReportInfo outSdCardFatReportInfo)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _fatReportMutex);

        outBisSystemFatReportInfo.FilePeakOpenCount = _bisSystemReport.FilePeakOpenCount;
        outBisSystemFatReportInfo.DirectoryPeakOpenCount = _bisSystemReport.DirectoryPeakOpenCount;
        outBisUserFatReportInfo.FilePeakOpenCount = _bisUserReport.FilePeakOpenCount;
        outBisUserFatReportInfo.DirectoryPeakOpenCount = _bisUserReport.DirectoryPeakOpenCount;
        outSdCardFatReportInfo.FilePeakOpenCount = _sdCardReport.FilePeakOpenCount;
        outSdCardFatReportInfo.DirectoryPeakOpenCount = _sdCardReport.DirectoryPeakOpenCount;

        _bisSystemReport.FilePeakOpenCount = _bisSystemReport.FileCurrentOpenCount;
        _bisSystemReport.DirectoryPeakOpenCount = _bisSystemReport.DirectoryCurrentOpenCount;
        _bisUserReport.FilePeakOpenCount = _bisUserReport.FileCurrentOpenCount;
        _bisUserReport.DirectoryPeakOpenCount = _bisUserReport.DirectoryCurrentOpenCount;
        _sdCardReport.FilePeakOpenCount = _sdCardReport.FileCurrentOpenCount;
        _sdCardReport.DirectoryPeakOpenCount = _sdCardReport.DirectoryCurrentOpenCount;
    }
}