using System;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
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

        public void Initialize(FileSystemClient _)
        {
            MutexForAccessLogInitialization.Initialize();
        }
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
        public static Result GetGlobalAccessLogMode(this FileSystemClient fs, out GlobalAccessLogMode mode)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return fsProxy.Target.GetGlobalAccessLogMode(out mode);
        }

        public static Result SetGlobalAccessLogMode(this FileSystemClient fs, GlobalAccessLogMode mode)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            return fsProxy.Target.SetGlobalAccessLogMode(mode);
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

        public static Result RunOperationWithAccessLog(this FileSystemClient fs, AccessLogTarget logTarget,
            Func<Result> operation, Func<string> textGenerator, [CallerMemberName] string caller = "")
        {
            Result rc;

            if (fs.Impl.IsEnabledAccessLog(logTarget))
            {
                Tick start = fs.Hos.Os.GetSystemTick();
                rc = operation();
                Tick end = fs.Hos.Os.GetSystemTick();

                fs.Impl.OutputAccessLog(rc, start, end, null, textGenerator().ToU8Span(), caller);
            }
            else
            {
                rc = operation();
            }

            return rc;
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

    internal readonly struct IdentifyAccessLogHandle
    {
        public readonly object Handle;

        private IdentifyAccessLogHandle(object handle) => Handle = handle;
        public static IdentifyAccessLogHandle MakeHandle(object handle) => new IdentifyAccessLogHandle(handle);
    }

    internal struct IdString
    {
        private Buffer32 _buffer;

        private ReadOnlySpan<byte> ToValueString(int value)
        {
            bool success = Utf8Formatter.TryFormat(value, _buffer.Bytes, out int length);
            Assert.True(success && length < _buffer.Bytes.Length);
            _buffer[length] = 0;

            return _buffer.Bytes;
        }

        public ReadOnlySpan<byte> ToString(Priority value)
        {
            switch (value)
            {
                case Priority.Realtime: return new[] { (byte)'R', (byte)'e', (byte)'a', (byte)'l', (byte)'t', (byte)'i', (byte)'m', (byte)'e' };
                case Priority.Normal: return new[] { (byte)'N', (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'l' };
                case Priority.Low: return new[] { (byte)'L', (byte)'o', (byte)'w' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(PriorityRaw value)
        {
            switch (value)
            {
                case PriorityRaw.Realtime: return new[] { (byte)'R', (byte)'e', (byte)'a', (byte)'l', (byte)'t', (byte)'i', (byte)'m', (byte)'e' };
                case PriorityRaw.Normal: return new[] { (byte)'N', (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'l' };
                case PriorityRaw.Low: return new[] { (byte)'L', (byte)'o', (byte)'w' };
                case PriorityRaw.Background: return new[] { (byte)'B', (byte)'a', (byte)'c', (byte)'k', (byte)'g', (byte)'r', (byte)'o', (byte)'u', (byte)'n', (byte)'d' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(ImageDirectoryId value)
        {
            switch (value)
            {
                case ImageDirectoryId.Nand: return new[] { (byte)'N', (byte)'a', (byte)'n', (byte)'d' };
                case ImageDirectoryId.SdCard: return new[] { (byte)'S', (byte)'d', (byte)'C', (byte)'a', (byte)'r', (byte)'d' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(ContentStorageId value)
        {
            switch (value)
            {
                case ContentStorageId.System: return new[] { (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m' };
                case ContentStorageId.User: return new[] { (byte)'U', (byte)'s', (byte)'e', (byte)'r' };
                case ContentStorageId.SdCard: return new[] { (byte)'S', (byte)'d', (byte)'C', (byte)'a', (byte)'r', (byte)'d' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(GameCardPartition value)
        {
            switch (value)
            {
                case GameCardPartition.Update: return new[] { (byte)'U', (byte)'p', (byte)'d', (byte)'a', (byte)'t', (byte)'e' };
                case GameCardPartition.Normal: return new[] { (byte)'N', (byte)'o', (byte)'r', (byte)'m', (byte)'a', (byte)'l' };
                case GameCardPartition.Secure: return new[] { (byte)'S', (byte)'e', (byte)'c', (byte)'u', (byte)'r', (byte)'e' };
                case GameCardPartition.Logo: return new[] { (byte)'L', (byte)'o', (byte)'g', (byte)'o' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(SaveDataSpaceId value)
        {
            switch (value)
            {
                case SaveDataSpaceId.System: return new[] { (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m' };
                case SaveDataSpaceId.User: return new[] { (byte)'U', (byte)'s', (byte)'e', (byte)'r' };
                case SaveDataSpaceId.SdSystem: return new[] { (byte)'S', (byte)'d', (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m' };
                case SaveDataSpaceId.ProperSystem: return new[] { (byte)'P', (byte)'r', (byte)'o', (byte)'p', (byte)'e', (byte)'r', (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(ContentType value)
        {
            switch (value)
            {
                case ContentType.Meta: return new[] { (byte)'M', (byte)'e', (byte)'t', (byte)'a' };
                case ContentType.Control: return new[] { (byte)'C', (byte)'o', (byte)'n', (byte)'t', (byte)'r', (byte)'o', (byte)'l' };
                case ContentType.Manual: return new[] { (byte)'M', (byte)'a', (byte)'n', (byte)'u', (byte)'a', (byte)'l' };
                case ContentType.Logo: return new[] { (byte)'L', (byte)'o', (byte)'g', (byte)'o' };
                case ContentType.Data: return new[] { (byte)'D', (byte)'a', (byte)'t', (byte)'a' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(BisPartitionId value)
        {
            switch (value)
            {
                case BisPartitionId.BootPartition1Root: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'i', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'1', (byte)'R', (byte)'o', (byte)'o', (byte)'t' };
                case BisPartitionId.BootPartition2Root: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'i', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'2', (byte)'R', (byte)'o', (byte)'o', (byte)'t' };
                case BisPartitionId.UserDataRoot: return new[] { (byte)'U', (byte)'s', (byte)'e', (byte)'r', (byte)'D', (byte)'a', (byte)'t', (byte)'a', (byte)'R', (byte)'o', (byte)'o', (byte)'t' };
                case BisPartitionId.BootConfigAndPackage2Part1: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f', (byte)'i', (byte)'g', (byte)'A', (byte)'n', (byte)'d', (byte)'P', (byte)'a', (byte)'c', (byte)'k', (byte)'a', (byte)'g', (byte)'e', (byte)'2', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'1' };
                case BisPartitionId.BootConfigAndPackage2Part2: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f', (byte)'i', (byte)'g', (byte)'A', (byte)'n', (byte)'d', (byte)'P', (byte)'a', (byte)'c', (byte)'k', (byte)'a', (byte)'g', (byte)'e', (byte)'2', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'2' };
                case BisPartitionId.BootConfigAndPackage2Part3: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f', (byte)'i', (byte)'g', (byte)'A', (byte)'n', (byte)'d', (byte)'P', (byte)'a', (byte)'c', (byte)'k', (byte)'a', (byte)'g', (byte)'e', (byte)'2', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'3' };
                case BisPartitionId.BootConfigAndPackage2Part4: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f', (byte)'i', (byte)'g', (byte)'A', (byte)'n', (byte)'d', (byte)'P', (byte)'a', (byte)'c', (byte)'k', (byte)'a', (byte)'g', (byte)'e', (byte)'2', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'4' };
                case BisPartitionId.BootConfigAndPackage2Part5: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f', (byte)'i', (byte)'g', (byte)'A', (byte)'n', (byte)'d', (byte)'P', (byte)'a', (byte)'c', (byte)'k', (byte)'a', (byte)'g', (byte)'e', (byte)'2', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'5' };
                case BisPartitionId.BootConfigAndPackage2Part6: return new[] { (byte)'B', (byte)'o', (byte)'o', (byte)'t', (byte)'C', (byte)'o', (byte)'n', (byte)'f', (byte)'i', (byte)'g', (byte)'A', (byte)'n', (byte)'d', (byte)'P', (byte)'a', (byte)'c', (byte)'k', (byte)'a', (byte)'g', (byte)'e', (byte)'2', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'6' };
                case BisPartitionId.CalibrationBinary: return new[] { (byte)'C', (byte)'a', (byte)'l', (byte)'i', (byte)'b', (byte)'r', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'B', (byte)'i', (byte)'n', (byte)'a', (byte)'r', (byte)'y' };
                case BisPartitionId.CalibrationFile: return new[] { (byte)'C', (byte)'a', (byte)'l', (byte)'i', (byte)'b', (byte)'r', (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'F', (byte)'i', (byte)'l', (byte)'e' };
                case BisPartitionId.SafeMode: return new[] { (byte)'S', (byte)'a', (byte)'f', (byte)'e', (byte)'M', (byte)'o', (byte)'d', (byte)'e' };
                case BisPartitionId.User: return new[] { (byte)'U', (byte)'s', (byte)'e', (byte)'r' };
                case BisPartitionId.System: return new[] { (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m' };
                case BisPartitionId.SystemProperEncryption: return new[] { (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m', (byte)'P', (byte)'r', (byte)'o', (byte)'p', (byte)'e', (byte)'r', (byte)'E', (byte)'n', (byte)'c', (byte)'r', (byte)'y', (byte)'p', (byte)'t', (byte)'i', (byte)'o', (byte)'n' };
                case BisPartitionId.SystemProperPartition: return new[] { (byte)'S', (byte)'y', (byte)'s', (byte)'t', (byte)'e', (byte)'m', (byte)'P', (byte)'r', (byte)'o', (byte)'p', (byte)'e', (byte)'r', (byte)'P', (byte)'a', (byte)'r', (byte)'t', (byte)'i', (byte)'t', (byte)'i', (byte)'o', (byte)'n' };
                case (BisPartitionId)35: return new[] { (byte)'I', (byte)'n', (byte)'v', (byte)'a', (byte)'l', (byte)'i', (byte)'d' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(DirectoryEntryType value)
        {
            switch (value)
            {
                case DirectoryEntryType.Directory: return new[] { (byte)'D', (byte)'i', (byte)'r', (byte)'e', (byte)'c', (byte)'t', (byte)'o', (byte)'r', (byte)'y' };
                case DirectoryEntryType.File: return new[] { (byte)'F', (byte)'i', (byte)'l', (byte)'e' };
                default: return ToValueString((int)value);
            }
        }

        public ReadOnlySpan<byte> ToString(MountHostOption value)
        {
            switch (value.Flags)
            {
                case MountHostOptionFlag.PseudoCaseSensitive: return new[] { (byte)'M', (byte)'o', (byte)'u', (byte)'n', (byte)'t', (byte)'H', (byte)'o', (byte)'s', (byte)'t', (byte)'O', (byte)'p', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'F', (byte)'l', (byte)'a', (byte)'g', (byte)'_', (byte)'P', (byte)'s', (byte)'e', (byte)'u', (byte)'d', (byte)'o', (byte)'C', (byte)'a', (byte)'s', (byte)'e', (byte)'S', (byte)'e', (byte)'n', (byte)'s', (byte)'i', (byte)'t', (byte)'i', (byte)'v', (byte)'e' };
                default: return ToValueString((int)value.Flags);
            }
        }
    }

    internal static class AccessLogImpl
    {
        internal static T DereferenceOutValue<T>(in T value, Result result) where T : unmanaged
        {
            return result.IsSuccess() ? value : default;
        }

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

        public static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            FileHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            throw new NotImplementedException();
        }

        public static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            DirectoryHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            throw new NotImplementedException();
        }

        public static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            IdentifyAccessLogHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            throw new NotImplementedException();
        }

        public static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            object handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            throw new NotImplementedException();
        }

        public static void OutputAccessLogToOnlySdCard(this FileSystemClientImpl fs, U8Span message)
        {
            throw new NotImplementedException();
        }

        public static void OutputAccessLogUnlessResultSuccess(this FileSystemClientImpl fs, Result result, Tick start,
            Tick end, FileHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            throw new NotImplementedException();
        }

        public static void OutputAccessLogUnlessResultSuccess(this FileSystemClientImpl fs, Result result, Tick start,
            Tick end, DirectoryHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            throw new NotImplementedException();
        }

        public static void OutputAccessLogUnlessResultSuccess(this FileSystemClientImpl fs, Result result, Tick start,
            Tick end, object handle, U8Span message, [CallerMemberName] string functionName = "")
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

        public static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, FileHandle handle)
        {
            if (handle.File is null)
                return true;

            FileSystemAccessor fsAccessor = handle.File.GetParent();
            return fsAccessor is not null && fsAccessor.IsEnabledAccessLog();
        }

        public static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, DirectoryHandle handle)
        {
            if (handle.Directory is null)
                return true;

            return handle.Directory.GetParent().IsEnabledAccessLog();
        }

        public static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, IdentifyAccessLogHandle handle)
        {
            return true;
        }

        public static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, object handle)
        {
            if (handle is null)
                return true;

            // We should never receive non-null here.
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            Assert.True(handle is null);
            return false;
        }

        public static bool IsEnabledFileSystemAccessorAccessLog(this FileSystemClientImpl fs, U8Span mountName)
        {
            throw new NotImplementedException();
        }

        public static void EnableFileSystemAccessorAccessLog(this FileSystemClientImpl fs, U8Span mountName)
        {
            throw new NotImplementedException();
        }

        public static void FlushAccessLog(this FileSystemClientImpl fs)
        {
            throw new NotImplementedException();
        }

        public static void FlushAccessLogOnSdCard(this FileSystemClientImpl fs)
        {
            throw new NotImplementedException();
        }

        public static ReadOnlySpan<byte> ConvertFromBoolToAccessLogBooleanValue(bool value)
        {
            return value ? AccessLogStrings.LogTrue : AccessLogStrings.LogFalse;
        }
    }

    internal static class AccessLogStrings
    {
        public static byte LogQuote => (byte)'"';

        public static ReadOnlySpan<byte> LogTrue => // "true"
            new[] { (byte)'t', (byte)'r', (byte)'u', (byte)'e' };

        public static ReadOnlySpan<byte> LogFalse => // "false"
            new[] { (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e' };

        public static ReadOnlySpan<byte> LogEntryBufferCount => // ", entry_buffer_count: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'e', (byte)'n', (byte)'t', (byte)'r', (byte)'y', (byte)'_',
                (byte)'b', (byte)'u', (byte)'f', (byte)'f', (byte)'e', (byte)'r', (byte)'_', (byte)'c',
                (byte)'o', (byte)'u', (byte)'n', (byte)'t', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogEntryCount => // ", entry_count: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'e', (byte)'n', (byte)'t', (byte)'r', (byte)'y', (byte)'_',
                (byte)'c', (byte)'o', (byte)'u', (byte)'n', (byte)'t', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogOffset => // ", offset: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'o', (byte)'f', (byte)'f', (byte)'s', (byte)'e', (byte)'t',
                (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogSize => // ", size: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'i', (byte)'z', (byte)'e', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogWriteOptionFlush => // ", write_option: Flush"
            new[]
            {
                (byte)',', (byte)' ', (byte)'w', (byte)'r', (byte)'i', (byte)'t', (byte)'e', (byte)'_',
                (byte)'o', (byte)'p', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)':', (byte)' ',
                (byte)'F', (byte)'l', (byte)'u', (byte)'s', (byte)'h'
            };

        public static ReadOnlySpan<byte> LogOpenMode => // ", open_mode: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'o', (byte)'p', (byte)'e', (byte)'n', (byte)'_', (byte)'m',
                (byte)'o', (byte)'d', (byte)'e', (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogPath => // ", path: ""
            new[]
            {
                (byte)',', (byte)' ', (byte)'p', (byte)'a', (byte)'t', (byte)'h', (byte)':', (byte)' ',
                (byte)'"'
            };

        public static ReadOnlySpan<byte> LogNewPath => // "", new_path: ""
            new[]
            {
                (byte)'"', (byte)',', (byte)' ', (byte)'n', (byte)'e', (byte)'w', (byte)'_', (byte)'p',
                (byte)'a', (byte)'t', (byte)'h', (byte)':', (byte)' ', (byte)'"'
            };

        public static ReadOnlySpan<byte> LogEntryType => // "", entry_type: "
            new[]
            {
                (byte)'"', (byte)',', (byte)' ', (byte)'e', (byte)'n', (byte)'t', (byte)'r', (byte)'y',
                (byte)'_', (byte)'t', (byte)'y', (byte)'p', (byte)'e', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogName => // ", name: ""
            new[]
            {
                (byte)',', (byte)' ', (byte)'n', (byte)'a', (byte)'m', (byte)'e', (byte)':', (byte)' ',
                (byte)'"'
            };

        public static ReadOnlySpan<byte> LogCommitOption => // "", commit_option: 0x"
            new[]
            {
                (byte)'"', (byte)',', (byte)' ', (byte)'c', (byte)'o', (byte)'m', (byte)'m', (byte)'i',
                (byte)'t', (byte)'_', (byte)'o', (byte)'p', (byte)'t', (byte)'i', (byte)'o', (byte)'n',
                (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogIsMounted => // "", is_mounted: ""
            new[]
            {
                (byte)'"', (byte)',', (byte)' ', (byte)'i', (byte)'s', (byte)'_', (byte)'m', (byte)'o',
                (byte)'u', (byte)'n', (byte)'t', (byte)'e', (byte)'d', (byte)':', (byte)' ', (byte)'"'
            };

        public static ReadOnlySpan<byte> LogApplicationId => // ", applicationid: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'a', (byte)'p', (byte)'p', (byte)'l', (byte)'i', (byte)'c',
                (byte)'a', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'i', (byte)'d', (byte)':',
                (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogProgramId => // ", programid: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'p', (byte)'r', (byte)'o', (byte)'g', (byte)'r', (byte)'a',
                (byte)'m', (byte)'i', (byte)'d', (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogDataId => // ", dataid: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'d', (byte)'a', (byte)'t', (byte)'a', (byte)'i', (byte)'d',
                (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogBisPartitionId => // ", bispartitionid: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'b', (byte)'i', (byte)'s', (byte)'p', (byte)'a', (byte)'r',
                (byte)'t', (byte)'i', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'i', (byte)'d',
                (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogContentType => // ", content_type: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'c', (byte)'o', (byte)'n', (byte)'t', (byte)'e', (byte)'n',
                (byte)'t', (byte)'_', (byte)'t', (byte)'y', (byte)'p', (byte)'e', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogContentStorageId => // ", contentstorageid: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'c', (byte)'o', (byte)'n', (byte)'t', (byte)'e', (byte)'n',
                (byte)'t', (byte)'s', (byte)'t', (byte)'o', (byte)'r', (byte)'a', (byte)'g', (byte)'e',
                (byte)'i', (byte)'d', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogGameCardHandle => // ", gamecard_handle: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'g', (byte)'a', (byte)'m', (byte)'e', (byte)'c', (byte)'a',
                (byte)'r', (byte)'d', (byte)'_', (byte)'h', (byte)'a', (byte)'n', (byte)'d', (byte)'l',
                (byte)'e', (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogGameCardPartition => // ", gamecard_partition: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'g', (byte)'a', (byte)'m', (byte)'e', (byte)'c', (byte)'a',
                (byte)'r', (byte)'d', (byte)'_', (byte)'p', (byte)'a', (byte)'r', (byte)'t', (byte)'i',
                (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogMountHostOption => // ", mount_host_option: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'m', (byte)'o', (byte)'u', (byte)'n', (byte)'t', (byte)'_',
                (byte)'h', (byte)'o', (byte)'s', (byte)'t', (byte)'_', (byte)'o', (byte)'p', (byte)'t',
                (byte)'i', (byte)'o', (byte)'n', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogRootPath => // ", root_path: ""
            new[]
            {
                (byte)',', (byte)' ', (byte)'r', (byte)'o', (byte)'o', (byte)'t', (byte)'_', (byte)'p',
                (byte)'a', (byte)'t', (byte)'h', (byte)':', (byte)' ', (byte)'"'
            };

        public static ReadOnlySpan<byte> LogUserId => // ", userid: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'u', (byte)'s', (byte)'e', (byte)'r', (byte)'i', (byte)'d',
                (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogIndex => // ", index: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'i', (byte)'n', (byte)'d', (byte)'e', (byte)'x', (byte)':',
                (byte)' '
            };

        public static ReadOnlySpan<byte> LogSaveDataOwnerId => // ", save_data_owner_id: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'_', (byte)'d',
                (byte)'a', (byte)'t', (byte)'a', (byte)'_', (byte)'o', (byte)'w', (byte)'n', (byte)'e',
                (byte)'r', (byte)'_', (byte)'i', (byte)'d', (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogSaveDataSize => // ", save_data_size: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'_', (byte)'d',
                (byte)'a', (byte)'t', (byte)'a', (byte)'_', (byte)'s', (byte)'i', (byte)'z', (byte)'e',
                (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogSaveDataJournalSize => // ", save_data_journal_size: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'_', (byte)'d',
                (byte)'a', (byte)'t', (byte)'a', (byte)'_', (byte)'j', (byte)'o', (byte)'u', (byte)'r',
                (byte)'n', (byte)'a', (byte)'l', (byte)'_', (byte)'s', (byte)'i', (byte)'z', (byte)'e',
                (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogSaveDataFlags => // ", save_data_flags: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'_', (byte)'d',
                (byte)'a', (byte)'t', (byte)'a', (byte)'_', (byte)'f', (byte)'l', (byte)'a', (byte)'g',
                (byte)'s', (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogSaveDataId => // ", savedataid: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'d', (byte)'a',
                (byte)'t', (byte)'a', (byte)'i', (byte)'d', (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogSaveDataSpaceId => // ", savedataspaceid: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'d', (byte)'a',
                (byte)'t', (byte)'a', (byte)'s', (byte)'p', (byte)'a', (byte)'c', (byte)'e', (byte)'i',
                (byte)'d', (byte)':', (byte)' '
            };
    }
}
