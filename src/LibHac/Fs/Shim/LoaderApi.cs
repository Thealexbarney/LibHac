using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Os;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for use by the Loader service.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class LoaderApi
{
    public static Result IsArchivedProgram(this FileSystemClient fs, out bool isArchived, ProcessId processId)
    {
        using SharedRef<IFileSystemProxyForLoader> fileSystemProxy =
            fs.Impl.GetFileSystemProxyForLoaderServiceObject();

        Result res = fileSystemProxy.Get.IsArchivedProgram(out isArchived, processId.Value);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}