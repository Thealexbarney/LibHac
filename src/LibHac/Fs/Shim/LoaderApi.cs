using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Os;

namespace LibHac.Fs.Shim;

public static class LoaderApi
{
    public static Result IsArchivedProgram(this FileSystemClient fs, out bool isArchived, ProcessId processId)
    {
        using SharedRef<IFileSystemProxyForLoader> fileSystemProxy =
            fs.Impl.GetFileSystemProxyForLoaderServiceObject();

        Result rc = fileSystemProxy.Get.IsArchivedProgram(out isArchived, processId.Value);
        fs.Impl.AbortIfNeeded(rc);
        return rc;
    }
}
