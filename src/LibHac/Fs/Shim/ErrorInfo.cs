using LibHac.Common;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for obtaining error information from the file system proxy service.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class ErrorInfo
{
    public static Result GetAndClearFileSystemProxyErrorInfo(this FileSystemClient fs,
        out FileSystemProxyErrorInfo outErrorInfo)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.GetAndClearErrorInfo(out outErrorInfo);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}