using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim
{
    public static class PosixTimeShim
    {
        public static Result SetCurrentPosixTime(this FileSystemClient fs, Time.PosixTime currentPosixTime,
            int timeDifferenceSeconds)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.SetCurrentPosixTimeWithTimeDifference(currentPosixTime.Value,
                timeDifferenceSeconds);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }
    }
}
