using System;
using System.Runtime.InteropServices;
using LibHac.Diag;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Os;

namespace LibHac.Fs
{
    internal struct AccessLogGlobals
    {
        public GlobalAccessLogMode GlobalAccessLogMode;
        public AccessLogTarget LocalAccessLogTarget;

        public bool IsAccessLogInitialized;
        public SdkMutexType MutexForAccessLogInitialization;
    }

    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    public struct ApplicationInfo
    {
        public Ncm.ApplicationId ApplicationId;
        public uint Version;
        public byte LaunchType;
        public bool IsMultiProgram;
    }

    [Flags]
    public enum GlobalAccessLogMode
    {
        None = 0,
        Log = 1 << 0,
        SdCard = 1 << 1,
        All = Log | SdCard
    }

    public static class AccessLog
    {
        private static bool HasFileSystemServer(FileSystemClient fs)
        {
            return fs.Hos is not null;
        }

        public static Result GetGlobalAccessLogMode(this FileSystemClient fs, out GlobalAccessLogMode mode)
        {
            if (HasFileSystemServer(fs))
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fsProxy.Target.GetGlobalAccessLogMode(out mode);
            }

            mode = fs.Globals.AccessLog.GlobalAccessLogMode;
            return Result.Success;
        }

        public static Result SetGlobalAccessLogMode(this FileSystemClient fs, GlobalAccessLogMode mode)
        {
            if (HasFileSystemServer(fs))
            {
                using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

                return fsProxy.Target.SetGlobalAccessLogMode(mode);
            }

            fs.Globals.AccessLog.GlobalAccessLogMode = mode;
            return Result.Success;
        }

        public static void SetLocalAccessLog(this FileSystemClient fs, bool enabled)
        {
            SetLocalAccessLogImpl(fs, enabled);
        }

        public static void SetLocalApplicationAccessLog(this FileSystemClient fs, bool enabled)
        {
            SetLocalAccessLogImpl(fs, enabled);
        }

        public static void SetLocalSystemAccessLogForDebug(this FileSystemClient fs, bool enabled)
        {
            if (enabled)
            {
                fs.Globals.AccessLog.LocalAccessLogTarget |= AccessLogTarget.All;
            }
            else
            {
                fs.Globals.AccessLog.LocalAccessLogTarget &= ~AccessLogTarget.All;
            }
        }

        private static void SetLocalAccessLogImpl(FileSystemClient fs, bool enabled)
        {
            if (enabled)
            {
                fs.Globals.AccessLog.LocalAccessLogTarget |= AccessLogTarget.Application;
            }
            else
            {
                fs.Globals.AccessLog.LocalAccessLogTarget &= ~AccessLogTarget.Application;
            }
        }
    }
}

namespace LibHac.Fs.Impl
{
    [Flags]
    public enum AccessLogTarget
    {
        None = 0,
        Application = 1 << 0,
        System = 1 << 1,
        All = Application | System
    }

    internal static class AccessLogImpl
    {
        private static void GetProgramIndexForAccessLog(FileSystemClient fs, out int index, out int count)
        {
            throw new NotImplementedException();
        }

        private static void OutputAccessLogStart(FileSystemClient fs)
        {
            throw new NotImplementedException();
        }

        private static void OutputAccessLogStartForSystem(FileSystemClient fs)
        {
            throw new NotImplementedException();
        }

        private static void OutputAccessLogStartGeneratedByCallback(FileSystemClient fs)
        {
            throw new NotImplementedException();
        }

        internal static bool IsEnabledAccessLog(this FileSystemClientImpl fs, AccessLogTarget target)
        {
            ref AccessLogGlobals g = ref fs.Globals.AccessLog;

            if ((g.LocalAccessLogTarget & target) == 0)
                return false;

            if (!g.IsAccessLogInitialized)
            {
                using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref g.MutexForAccessLogInitialization);

                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                if (!g.IsAccessLogInitialized)
                {
                    if (g.LocalAccessLogTarget.HasFlag(AccessLogTarget.System))
                    {
                        g.GlobalAccessLogMode = GlobalAccessLogMode.Log;
                        OutputAccessLogStartForSystem(fs.Fs);
                        OutputAccessLogStartGeneratedByCallback(fs.Fs);
                    }
                    else
                    {
                        Result rc = fs.Fs.GetGlobalAccessLogMode(out g.GlobalAccessLogMode);
                        if (rc.IsFailure()) Abort.DoAbort(rc);

                        if (g.GlobalAccessLogMode != GlobalAccessLogMode.None)
                        {
                            OutputAccessLogStart(fs.Fs);
                            OutputAccessLogStartGeneratedByCallback(fs.Fs);
                        }
                    }

                    g.IsAccessLogInitialized = true;
                }
            }

            return g.GlobalAccessLogMode != GlobalAccessLogMode.None;
        }

        internal static bool IsEnabledAccessLog(this FileSystemClientImpl fs)
        {
            return fs.IsEnabledAccessLog(AccessLogTarget.All);
        }
    }
}
