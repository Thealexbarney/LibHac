using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Sf;
using FileSystemAccessor = LibHac.Fs.Accessors.FileSystemAccessor;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        private GlobalAccessLogMode GlobalAccessLogMode { get; set; }
        private AccessLogTarget AccessLogTarget { get; set; }
        private bool AccessLogInitialized { get; set; }

        private readonly object _accessLogInitLocker = new object();

        public void SetAccessLogObject(IAccessLog accessLog)
        {
            AccessLog = accessLog;
        }

        internal bool IsEnabledAccessLog(AccessLogTarget target)
        {
            if ((AccessLogTarget & target) == 0)
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
                        using ReferenceCountedDisposable<IFileSystemProxy> fsProxy =
                            Impl.GetFileSystemProxyServiceObject();

                        Result rc = fsProxy.Target.GetGlobalAccessLogMode(out GlobalAccessLogMode globalMode);
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

        internal void EnableFileSystemAccessorAccessLog(U8Span mountName)
        {
            if (MountTable.Find(mountName.ToString(), out FileSystemAccessor accessor).IsFailure())
            {
                throw new LibHacException("abort");
            }

            accessor.IsAccessLogEnabled = true;
        }

        internal void OutputAccessLog(Result result, System.TimeSpan startTime, System.TimeSpan endTime, string message, [CallerMemberName] string caller = "")
        {
            OutputAccessLogImpl(result, startTime, endTime, 0, message, caller);
        }

        internal void OutputAccessLogUnlessResultSuccess(Result result, System.TimeSpan startTime, System.TimeSpan endTime, string message, [CallerMemberName] string caller = "")
        {
            if (result.IsFailure())
            {
                OutputAccessLogImpl(result, startTime, endTime, 0, message, caller);
            }
        }

        internal void OutputAccessLogImpl(Result result, System.TimeSpan startTime, System.TimeSpan endTime, int handleId,
            string message, [CallerMemberName] string caller = "")
        {
            if (GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.Log))
            {
                AccessLog?.Log(result, startTime, endTime, handleId, message, caller);
            }

            if (GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.SdCard))
            {
                string logString = AccessLogHelpers.BuildDefaultLogLine(result, startTime, endTime, handleId, message, caller);

                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = Impl.GetFileSystemProxyServiceObject();
                fsProxy.Target.OutputAccessLogToSdCard(new InBuffer(logString.ToU8Span())).IgnoreResult();
            }
        }

        public Result RunOperationWithAccessLog(AccessLogTarget logTarget, Func<Result> operation,
            Func<string> textGenerator, [CallerMemberName] string caller = "")
        {
            Result rc;

            if (IsEnabledAccessLog(logTarget))
            {
                System.TimeSpan startTime = Time.GetCurrent();
                rc = operation();
                System.TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, textGenerator(), caller);
            }
            else
            {
                rc = operation();
            }

            return rc;
        }

        public Result RunOperationWithAccessLogOnFailure(AccessLogTarget logTarget, Func<Result> operation,
            Func<string> textGenerator, [CallerMemberName] string caller = "")
        {
            Result rc;

            if (IsEnabledAccessLog(logTarget))
            {
                System.TimeSpan startTime = Time.GetCurrent();
                rc = operation();
                System.TimeSpan endTime = Time.GetCurrent();

                OutputAccessLogUnlessResultSuccess(rc, startTime, endTime, textGenerator(), caller);
            }
            else
            {
                rc = operation();
            }

            return rc;
        }
    }
}
