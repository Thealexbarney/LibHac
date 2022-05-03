using LibHac.Common;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for obtaining reports on memory usage from the file system proxy service.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class MemoryReportInfoShim
{
    public static Result GetAndClearMemoryReportInfo(this FileSystemClient fs, out MemoryReportInfo reportInfo)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.GetAndClearMemoryReportInfo(out reportInfo);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}