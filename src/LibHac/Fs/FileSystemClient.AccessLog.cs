using System;
using System.Runtime.CompilerServices;
using LibHac.FsService;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        private GlobalAccessLogMode GlobalAccessLogMode { get; set; }
        private LocalAccessLogMode LocalAccessLogMode { get; set; }
        private bool AccessLogInitialized { get; set; }

        private readonly object _accessLogInitLocker = new object();

        public Result GetGlobalAccessLogMode(out GlobalAccessLogMode mode)
        {
            IFileSystemProxy fsProxy = GetFileSystemProxyServiceObject();

            return fsProxy.GetGlobalAccessLogMode(out mode);
        }

        public Result SetGlobalAccessLogMode(GlobalAccessLogMode mode)
        {
            IFileSystemProxy fsProxy = GetFileSystemProxyServiceObject();

            return fsProxy.SetGlobalAccessLogMode(mode);
        }

        public void SetLocalAccessLogMode(LocalAccessLogMode mode)
        {
            LocalAccessLogMode = mode;
        }

        internal bool IsEnabledAccessLog(LocalAccessLogMode mode)
        {
            if ((LocalAccessLogMode & mode) == 0)
            {
                return false;
            }

            if (AccessLogInitialized)
            {
                return GlobalAccessLogMode != GlobalAccessLogMode.None;
            }

            lock (_accessLogInitLocker)
            {
                if (!AccessLogInitialized)
                {
                    IFileSystemProxy fsProxy = GetFileSystemProxyServiceObject();

                    Result rc = fsProxy.GetGlobalAccessLogMode(out GlobalAccessLogMode globalMode);
                    GlobalAccessLogMode = globalMode;

                    if (rc.IsFailure())
                    {
                        throw new LibHacException("Abort");
                    }

                    if (GlobalAccessLogMode != GlobalAccessLogMode.None)
                    {
                        InitAccessLog();
                    }

                    AccessLogInitialized = true;
                }
            }

            return GlobalAccessLogMode != GlobalAccessLogMode.None;
        }

        private void InitAccessLog()
        {

        }

        public Result RunOperationWithAccessLog(LocalAccessLogMode logType, Func<Result> operation, Func<string> textGenerator, [CallerMemberName] string caller = "")
        {
            Result rc;

            if (IsEnabledAccessLog(logType))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = operation();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, textGenerator(), caller);
            }
            else
            {
                rc = operation();
            }

            return rc;
        }
    }

    [Flags]
    public enum LocalAccessLogMode
    {
        None = 0,
        Application = 1 << 0,
        Internal = 1 << 1,
        All = Application | Internal
    }

    [Flags]
    public enum GlobalAccessLogMode
    {
        None = 0,
        Log = 1 << 0,
        SdCard = 1 << 1,
        All = Log | SdCard
    }
}
