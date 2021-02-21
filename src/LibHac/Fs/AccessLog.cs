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
            switch (value)
            {
                case MountHostOption.PseudoCaseSensitive: return new[] { (byte)'M', (byte)'o', (byte)'u', (byte)'n', (byte)'t', (byte)'H', (byte)'o', (byte)'s', (byte)'t', (byte)'O', (byte)'p', (byte)'t', (byte)'i', (byte)'o', (byte)'n', (byte)'F', (byte)'l', (byte)'a', (byte)'g', (byte)'_', (byte)'P', (byte)'s', (byte)'e', (byte)'u', (byte)'d', (byte)'o', (byte)'C', (byte)'a', (byte)'s', (byte)'e', (byte)'S', (byte)'e', (byte)'n', (byte)'s', (byte)'i', (byte)'t', (byte)'i', (byte)'v', (byte)'e' };
                default: return ToValueString((int)value);
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
            return value ? LogTrue : LogFalse;
        }

        private static ReadOnlySpan<byte> LogTrue => // "true"
            new[] { (byte)'t', (byte)'r', (byte)'u', (byte)'e' };

        private static ReadOnlySpan<byte> LogFalse => // "false"
            new[]
            {
                (byte)'f', (byte)'a', (byte)'l', (byte)'s', (byte)'e'
            };
    }
}
