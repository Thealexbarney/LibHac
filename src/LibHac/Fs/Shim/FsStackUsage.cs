using LibHac.Common;
using LibHac.Diag;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for getting the amount of stack space used by FS threads.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class FsStackUsage
{
    public static uint GetFsStackUsage(this FileSystemClient fs, FsStackUsageThreadType threadType)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Abort.DoAbortUnlessSuccess(fileSystemProxy.Get.GetFsStackUsage(out uint stackUsage, threadType));

        return stackUsage;
    }
}