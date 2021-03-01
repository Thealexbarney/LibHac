using LibHac.FsSrv.Sf;
using LibHac.Os;

namespace LibHac.Fs.Shim
{
    public static class LoaderApi
    {
        public static Result IsArchivedProgram(this FileSystemClient fs, out bool isArchived, ProcessId processId)
        {
            using ReferenceCountedDisposable<IFileSystemProxyForLoader> fsProxy =
                fs.Impl.GetFileSystemProxyForLoaderServiceObject();

            Result rc = fsProxy.Target.IsArchivedProgram(out isArchived, processId.Value);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }
    }
}
