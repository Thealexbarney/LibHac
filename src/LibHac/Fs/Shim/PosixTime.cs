using LibHac.Common;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Shim
{
    public static class PosixTimeShim
    {
        public static Result SetCurrentPosixTime(this FileSystemClient fs, Time.PosixTime currentPosixTime,
            int timeDifferenceSeconds)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fileSystemProxy.Get.SetCurrentPosixTimeWithTimeDifference(currentPosixTime.Value,
                timeDifferenceSeconds);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }
    }
}
