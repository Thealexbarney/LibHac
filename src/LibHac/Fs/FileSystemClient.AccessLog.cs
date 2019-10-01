using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Accessors;
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
            if (HasFileSystemServer())
            {
                IFileSystemProxy fsProxy = GetFileSystemProxyServiceObject();

                return fsProxy.GetGlobalAccessLogMode(out mode);
            }

            mode = GlobalAccessLogMode;
            return Result.Success;
        }

        public Result SetGlobalAccessLogMode(GlobalAccessLogMode mode)
        {
            if (HasFileSystemServer())
            {
                IFileSystemProxy fsProxy = GetFileSystemProxyServiceObject();

                return fsProxy.SetGlobalAccessLogMode(mode);
            }

            GlobalAccessLogMode = mode;
            return Result.Success;
        }

        public void SetLocalAccessLogMode(LocalAccessLogMode mode)
        {
            LocalAccessLogMode = mode;
        }

        public void SetAccessLogObject(IAccessLog accessLog)
        {
            AccessLog = accessLog;
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
                    if (HasFileSystemServer())
                    {
                        IFileSystemProxy fsProxy = GetFileSystemProxyServiceObject();

                        Result rc = fsProxy.GetGlobalAccessLogMode(out GlobalAccessLogMode globalMode);
                        GlobalAccessLogMode = globalMode;

                        if (rc.IsFailure())
                        {
                            throw new LibHacException("Abort");
                        }
                    }
                    else
                    {
                        GlobalAccessLogMode = GlobalAccessLogMode.Log;
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

        internal bool IsEnabledAccessLog()
        {
            return IsEnabledAccessLog(LocalAccessLogMode.All);
        }

        internal bool IsEnabledFileSystemAccessorAccessLog(string mountName)
        {
            if (MountTable.Find(mountName, out FileSystemAccessor accessor).IsFailure())
            {
                return true;
            }

            return accessor.IsAccessLogEnabled;
        }

        internal bool IsEnabledHandleAccessLog(FileHandle handle)
        {
            return handle.File.Parent.IsAccessLogEnabled;
        }

        internal bool IsEnabledHandleAccessLog(DirectoryHandle handle)
        {
            return handle.Directory.Parent.IsAccessLogEnabled;
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, string message, [CallerMemberName] string caller = "")
        {
            OutputAccessLogImpl(result, startTime, endTime, 0, message, caller);
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, FileHandle handle, string message, [CallerMemberName] string caller = "")
        {
            OutputAccessLogImpl(result, startTime, endTime, handle.GetId(), message, caller);
        }

        internal void OutputAccessLog(Result result, TimeSpan startTime, TimeSpan endTime, DirectoryHandle handle, string message, [CallerMemberName] string caller = "")
        {
            OutputAccessLogImpl(result, startTime, endTime, handle.GetId(), message, caller);
        }

        internal void OutputAccessLogImpl(Result result, TimeSpan startTime, TimeSpan endTime, int handleId,
            string message, [CallerMemberName] string caller = "")
        {
            if (GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.Log))
            {
                AccessLog?.Log(result, startTime, endTime, handleId, message, caller);
            }

            if (GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.SdCard))
            {
                string logString = AccessLogHelpers.BuildDefaultLogLine(result, startTime, endTime, handleId, message, caller);

                IFileSystemProxy fsProxy = GetFileSystemProxyServiceObject();
                fsProxy.OutputAccessLogToSdCard(logString.ToU8Span());
            }
        }

        public Result RunOperationWithAccessLog(LocalAccessLogMode logType, Func<Result> operation,
            Func<string> textGenerator, [CallerMemberName] string caller = "")
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

        public Result RunOperationWithAccessLog(LocalAccessLogMode logType, FileHandle handle, Func<Result> operation,
            Func<string> textGenerator, [CallerMemberName] string caller = "")
        {
            Result rc;

            if (IsEnabledAccessLog(logType) && handle.File.Parent.IsAccessLogEnabled)
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = operation();
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, textGenerator(), caller);
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
