using System;
using System.Buffers;
using System.Buffers.Text;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Os;
using LibHac.Sf;
using static LibHac.Fs.Impl.AccessLogStrings;

namespace LibHac.Fs
{
    internal struct AccessLogGlobals
    {
        public GlobalAccessLogMode GlobalAccessLogMode;
        public AccessLogTarget LocalAccessLogTarget;

        public bool IsServerless;
        public bool IsAccessLogInitialized;
        public SdkMutexType MutexForAccessLogInitialization;

        public AccessLogImpl.AccessLogPrinterCallbackManager CallbackManager;

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
            // Allow the access log to be used without an FS server by storing the mode locally in that situation.
            if (fs.Globals.AccessLog.IsServerless)
            {
                mode = fs.Globals.AccessLog.GlobalAccessLogMode;
                return Result.Success;
            }

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.GetGlobalAccessLogMode(out mode);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }

        public static Result SetGlobalAccessLogMode(this FileSystemClient fs, GlobalAccessLogMode mode)
        {
            // Allow the access log to be used without an FS server by storing the mode locally in that situation.
            if (fs.Globals.AccessLog.IsServerless)
            {
                fs.Globals.AccessLog.GlobalAccessLogMode = mode;
                return Result.Success;
            }

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result rc = fsProxy.Target.SetGlobalAccessLogMode(mode);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
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

        /// <summary>
        /// Sets whether the FS access log should call the FS service when getting or setting the
        /// global access log mode. This allows the access log to be used when using an FS client without a server.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="isServerless">Does this client lack an FS server?</param>
        public static void SetServerlessAccessLog(this FileSystemClient fs, bool isServerless)
        {
            fs.Globals.AccessLog.IsServerless = isServerless;
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

        public static void OutputApplicationInfoAccessLog(this FileSystemClient fs, in ApplicationInfo applicationInfo)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();
            fsProxy.Target.OutputApplicationInfoAccessLog(in applicationInfo).IgnoreResult();
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
            Assert.SdkAssert(success);
            Assert.SdkLess(length, _buffer.Bytes.Length);
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

    internal delegate int AccessLogPrinterCallback(Span<byte> textBuffer);

    public static class AccessLogImpl
    {
        internal static T DereferenceOutValue<T>(in T value, Result result) where T : unmanaged
        {
            return result.IsSuccess() ? value : default;
        }

        private static U8Span GetPriorityRawName(FileSystemClientImpl fs, ref IdString idString)
        {
            // Note: The original function creates an IdString instead of taking one as a parameter.
            // Because this might result in the function returning a string from its own stack frame,
            // this implementation takes an IdString as a parameter.
            return new U8Span(idString.ToString(fs.Fs.GetPriorityRawOnCurrentThreadForInternalUse()));
        }

        private static ref AccessLogPrinterCallbackManager GetStartAccessLogPrinterCallbackManager(
            FileSystemClientImpl fs)
        {
            return ref fs.Globals.AccessLog.CallbackManager;
        }

        private static void FlushAccessLogOnSdCardImpl(FileSystemClientImpl fs)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            fsProxy.Target.FlushAccessLogOnSdCard().IgnoreResult();
        }

        private static void OutputAccessLogToSdCardImpl(FileSystemClientImpl fs, U8Span message)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            fsProxy.Target.OutputAccessLogToSdCard(new InBuffer(message.Value)).IgnoreResult();
        }

        // The original function creates a string using the input format string and format args.
        // We're not doing that in C#, so just pass the message through.
        private static void OutputAccessLogToSdCard(this FileSystemClientImpl fs, U8Span message)
        {
            if (fs.Globals.AccessLog.GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.SdCard))
            {
                OutputAccessLogToSdCardImpl(fs, message);
            }
        }

        private static void OutputAccessLog(this FileSystemClientImpl fs, Result result, U8Span priority, Tick start,
            Tick end, string functionName, object handle, U8Span message)
        {
            // ReSharper disable once RedundantAssignment
            Span<byte> logBuffer = stackalloc byte[0];
            Span<byte> nameBuffer = stackalloc byte[0];

            // 1.5x the UTF-16 char length should probably be enough.
            int nameBufferSize = Math.Max(0x10, functionName.Length + (functionName.Length >> 1));

            // Go straight to the fallback code if the function name is too long.
            // Although to be honest, if this happens, saving on some allocations is probably the least of your worries.
            if (nameBufferSize <= 0x400)
            {
                nameBuffer = stackalloc byte[nameBufferSize];
                OperationStatus status = Utf8.FromUtf16(functionName, nameBuffer, out _, out int bytesWritten);

                // Set the length to 0 if the buffer is too small to signify it should be handled by the fallback code.
                // This will most likely never happen unless the function name has a lot of non-ASCII characters.
                if (status == OperationStatus.DestinationTooSmall)
                    nameBuffer = Span<byte>.Empty;
                else
                    nameBuffer = nameBuffer.Slice(0, bytesWritten);
            }

            if (nameBuffer.Length == 0 && functionName.Length != 0)
            {
                nameBuffer = Encoding.UTF8.GetBytes(functionName);
            }

            // Because the message is passed in as preformatted bytes instead of using sprintf like in the original,
            // we can easily calculate the size of the buffer needed beforehand.
            // The base text length is ~0x80-0x90 bytes long. Add another 0x70 as buffer space to get 0x100.
            const int baseLength = 0x100;
            int functionNameLength = nameBuffer.Length;
            int logBufferSize = baseLength + functionNameLength + message.Length;

            // In case we need to rent an array.
            RentedArray<byte> rentedLogBuffer = default;

            try
            {
                // Use the stack for buffers up to 1 KB since .NET stack sizes are usually massive anyway.
                if (logBufferSize <= 0x400)
                {
                    logBuffer = stackalloc byte[logBufferSize];
                }
                else
                {
                    rentedLogBuffer = new RentedArray<byte>(logBufferSize);
                    logBuffer = rentedLogBuffer.Array;
                }

                OsState os = fs.Hos.Os;
                long startMs = start.ToTimeSpan(os).GetMilliSeconds();
                long endMs = end.ToTimeSpan(os).GetMilliSeconds();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogLineStart)
                    .Append(LogStart).PadLeft((byte)' ', 9).AppendFormat(startMs)
                    .Append(LogEnd).PadLeft((byte)' ', 9).AppendFormat(endMs)
                    .Append(LogResult).AppendFormat(result.Value, 'x', 8)
                    .Append(LogHandle).AppendFormat((uint)(handle?.GetHashCode() ?? 0), 'x',
                        (byte)(Unsafe.SizeOf<nint>() * 2))
                    .Append(LogPriority).Append(priority)
                    .Append(LogFunction).Append(nameBuffer).Append(LogQuote)
                    .Append(message)
                    .Append(LogLineEnd);

                OutputAccessLogImpl(fs, new U8Span(sb.Buffer));

            }
            finally
            {
                rentedLogBuffer.Dispose();
            }
        }

        private static void GetProgramIndexForAccessLog(FileSystemClientImpl fs, out int index, out int count)
        {
            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.GetFileSystemProxyServiceObject();
            Result rc = fsProxy.Target.GetProgramIndexForAccessLog(out index, out count);
            Abort.DoAbortUnless(rc.IsSuccess());
        }

        private static void OutputAccessLogStart(FileSystemClientImpl fs)
        {
            Span<byte> logBuffer = stackalloc byte[0x80];

            GetProgramIndexForAccessLog(fs, out int currentProgramIndex, out int programCount);

            var sb = new U8StringBuilder(logBuffer, true);

            if (programCount > 1)
            {
                sb.Append(LogLineStart).Append(LogSdkVersion).Append(LogLibHacVersion).Append(LogSpec).Append(LogNx)
                    .Append(LogProgramIndex).AppendFormat(currentProgramIndex).Append(LogLineEnd);
            }
            else
            {
                sb.Append(LogLineStart).Append(LogSdkVersion).Append(LogLibHacVersion).Append(LogSpec).Append(LogNx)
                    .Append(LogLineEnd);
            }

            OutputAccessLogImpl(fs, new U8Span(sb.Buffer.Slice(0, sb.Length)));
        }

        private static void OutputAccessLogStartForSystem(FileSystemClientImpl fs)
        {
            Span<byte> logBuffer = stackalloc byte[0x60];

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogLineStart).Append(LogSdkVersion).Append(LogLibHacVersion).Append(LogSpec).Append(LogNx)
                .Append(LogForSystem).Append(LogLineEnd);

            OutputAccessLogImpl(fs, new U8Span(sb.Buffer.Slice(0, sb.Length)));
        }

        private static void OutputAccessLogStartGeneratedByCallback(FileSystemClientImpl fs)
        {
            ref AccessLogPrinterCallbackManager manager = ref GetStartAccessLogPrinterCallbackManager(fs);

            if (manager.IsRegisteredCallback())
            {
                Span<byte> logBuffer = stackalloc byte[0x80];
                int length = manager.InvokeCallback(logBuffer);

                if (length <= logBuffer.Length)
                {
                    OutputAccessLogImpl(fs, new U8Span(logBuffer.Slice(0, length)));
                }
            }
        }

        /// <summary>
        /// Outputs the provided message to the access log. <paramref name="message"/> should be trimmed to the length
        /// of the message text, and should not be null-terminated.
        /// </summary>
        /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
        /// <param name="message">The message to output to the access log.</param>
        private static void OutputAccessLogImpl(FileSystemClientImpl fs, U8Span message)
        {
            if (fs.Globals.AccessLog.GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.Log))
            {
                fs.Hos.Diag.Impl.LogImpl(FsModuleName, LogSeverity.Info, message);
            }

            if (fs.Globals.AccessLog.GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.SdCard))
            {
                OutputAccessLogToSdCardImpl(fs, message.Slice(0, message.Length - 1));
            }
        }

        internal struct AccessLogPrinterCallbackManager
        {
            private AccessLogPrinterCallback _callback;

            public bool IsRegisteredCallback()
            {
                return _callback is not null;
            }

            public void RegisterCallback(AccessLogPrinterCallback callback)
            {
                Assert.SdkNull(_callback);
                _callback = callback;
            }

            public int InvokeCallback(Span<byte> textBuffer)
            {
                Assert.SdkAssert(IsRegisteredCallback());
                return _callback(textBuffer);
            }
        }

        internal static void RegisterStartAccessLogPrinterCallback(FileSystemClientImpl fs,
            AccessLogPrinterCallback callback)
        {
            ref AccessLogPrinterCallbackManager manager = ref GetStartAccessLogPrinterCallbackManager(fs);
            manager.RegisterCallback(callback);
        }

        internal static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Priority priority, Tick start,
            Tick end, object handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            var idString = new IdString();
            OutputAccessLog(fs, result, new U8Span(idString.ToString(priority)), start, end, functionName, handle,
                message);
        }

        internal static void OutputAccessLog(this FileSystemClientImpl fs, Result result, PriorityRaw priorityRaw,
            Tick start, Tick end, object handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            var idString = new IdString();
            OutputAccessLog(fs, result, new U8Span(idString.ToString(priorityRaw)), start, end, functionName, handle,
                message);
        }

        internal static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            FileHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            var idString = new IdString();
            OutputAccessLog(fs, result, GetPriorityRawName(fs, ref idString), start, end, functionName, handle.File,
                message);
        }

        internal static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            DirectoryHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            var idString = new IdString();
            OutputAccessLog(fs, result, GetPriorityRawName(fs, ref idString), start, end, functionName,
                handle.Directory, message);
        }

        internal static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            IdentifyAccessLogHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            var idString = new IdString();
            OutputAccessLog(fs, result, GetPriorityRawName(fs, ref idString), start, end, functionName, handle.Handle,
                message);
        }

        internal static void OutputAccessLog(this FileSystemClientImpl fs, Result result, Tick start, Tick end,
            object handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            var idString = new IdString();
            OutputAccessLog(fs, result, GetPriorityRawName(fs, ref idString), start, end, functionName, handle,
                message);
        }

        internal static void OutputAccessLogToOnlySdCard(this FileSystemClientImpl fs, U8Span message)
        {
            fs.OutputAccessLogToSdCard(message);
        }

        internal static void OutputAccessLogUnlessResultSuccess(this FileSystemClientImpl fs, Result result, Tick start,
            Tick end, FileHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            if (result.IsFailure())
            {
                var idString = new IdString();
                OutputAccessLog(fs, result, GetPriorityRawName(fs, ref idString), start, end, functionName, handle,
                    message);
            }
        }

        internal static void OutputAccessLogUnlessResultSuccess(this FileSystemClientImpl fs, Result result, Tick start,
            Tick end, DirectoryHandle handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            if (result.IsFailure())
            {
                var idString = new IdString();
                OutputAccessLog(fs, result, GetPriorityRawName(fs, ref idString), start, end, functionName, handle,
                    message);
            }
        }

        internal static void OutputAccessLogUnlessResultSuccess(this FileSystemClientImpl fs, Result result, Tick start,
            Tick end, object handle, U8Span message, [CallerMemberName] string functionName = "")
        {
            if (result.IsFailure())
            {
                var idString = new IdString();
                OutputAccessLog(fs, result, GetPriorityRawName(fs, ref idString), start, end, functionName, handle,
                    message);
            }
        }

        internal static bool IsEnabledAccessLog(this FileSystemClientImpl fs, AccessLogTarget target)
        {
            ref AccessLogGlobals g = ref fs.Globals.AccessLog;

            if ((g.LocalAccessLogTarget & target) == 0)
                return false;

            if (g.IsAccessLogInitialized)
                return g.GlobalAccessLogMode != GlobalAccessLogMode.None;

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref g.MutexForAccessLogInitialization);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!g.IsAccessLogInitialized)
            {
                if (g.LocalAccessLogTarget.HasFlag(AccessLogTarget.System))
                {
                    g.GlobalAccessLogMode = GlobalAccessLogMode.Log;
                    OutputAccessLogStartForSystem(fs);
                    OutputAccessLogStartGeneratedByCallback(fs);
                }
                else
                {
                    Result rc = fs.Fs.GetGlobalAccessLogMode(out g.GlobalAccessLogMode);
                    fs.LogErrorMessage(rc);
                    if (rc.IsFailure()) Abort.DoAbort(rc);

                    if (g.GlobalAccessLogMode != GlobalAccessLogMode.None)
                    {
                        OutputAccessLogStart(fs);
                        OutputAccessLogStartGeneratedByCallback(fs);
                    }
                }

                g.IsAccessLogInitialized = true;
            }

            return g.GlobalAccessLogMode != GlobalAccessLogMode.None;
        }

        internal static bool IsEnabledAccessLog(this FileSystemClientImpl fs)
        {
            return fs.IsEnabledAccessLog(AccessLogTarget.All);
        }

        internal static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, FileHandle handle)
        {
            if (handle.File is null)
                return true;

            FileSystemAccessor fsAccessor = handle.File.GetParent();
            return fsAccessor is not null && fsAccessor.IsEnabledAccessLog();
        }

        internal static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, DirectoryHandle handle)
        {
            if (handle.Directory is null)
                return true;

            return handle.Directory.GetParent().IsEnabledAccessLog();
        }

        internal static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, IdentifyAccessLogHandle handle)
        {
            return true;
        }

        internal static bool IsEnabledHandleAccessLog(this FileSystemClientImpl _, object handle)
        {
            if (handle is null)
                return true;

            // We should never receive non-null here.
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            Assert.SdkAssert(handle is null,
                "Handle type must be FileHandle or DirectoryHandle. Please cast to handle type.");
            return false;
        }

        internal static bool IsEnabledFileSystemAccessorAccessLog(this FileSystemClientImpl fs, U8Span mountName)
        {
            Result rc = fs.Find(out FileSystemAccessor accessor, mountName);

            if (rc.IsFailure())
                return true;

            return accessor.IsEnabledAccessLog();
        }

        public static void EnableFileSystemAccessorAccessLog(this FileSystemClientImpl fs, U8Span mountName)
        {
            Result rc = fs.Find(out FileSystemAccessor fileSystem, mountName);
            fs.LogErrorMessage(rc);
            Abort.DoAbortUnless(rc.IsSuccess());

            fileSystem.SetAccessLog(true);
        }

        internal static void FlushAccessLog(this FileSystemClientImpl fs)
        {
            Assert.SdkAssert(false, $"Unsupported {nameof(FlushAccessLog)}");
        }

        internal static void FlushAccessLogOnSdCard(this FileSystemClientImpl fs)
        {
            if (fs.Globals.AccessLog.GlobalAccessLogMode.HasFlag(GlobalAccessLogMode.SdCard))
            {
                FlushAccessLogOnSdCardImpl(fs);
            }
        }

        internal static ReadOnlySpan<byte> ConvertFromBoolToAccessLogBooleanValue(bool value)
        {
            return value ? LogTrue : LogFalse;
        }
    }

    internal static class AccessLogStrings
    {
        public static ReadOnlySpan<byte> FsModuleName => // "$fs"
            new[] { (byte)'$', (byte)'f', (byte)'s' };

        public static ReadOnlySpan<byte> LogLibHacVersion => // "0.13.0"
            new[]
            {
                (byte)'0', (byte)'.', (byte)'1', (byte)'3', (byte)'.', (byte)'0'
            };

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

        public static ReadOnlySpan<byte> LogReadSize => // ", read_size: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'r', (byte)'e', (byte)'a', (byte)'d', (byte)'_', (byte)'s',
                (byte)'i', (byte)'z', (byte)'e', (byte)':', (byte)' '
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

        public static ReadOnlySpan<byte> LogSaveDataTimeStamp => // ", save_data_time_stamp: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'_', (byte)'d',
                (byte)'a', (byte)'t', (byte)'a', (byte)'_', (byte)'t', (byte)'i', (byte)'m', (byte)'e',
                (byte)'_', (byte)'s', (byte)'t', (byte)'a', (byte)'m', (byte)'p', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogSaveDataCommitId => // ", save_data_commit_id: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'_', (byte)'d',
                (byte)'a', (byte)'t', (byte)'a', (byte)'_', (byte)'c', (byte)'o', (byte)'m', (byte)'m',
                (byte)'i', (byte)'t', (byte)'_', (byte)'i', (byte)'d', (byte)':', (byte)' ', (byte)'0',
                (byte)'x'
            };

        public static ReadOnlySpan<byte> LogRestoreFlag => // ", restore_flag: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'r', (byte)'e', (byte)'s', (byte)'t', (byte)'o', (byte)'r',
                (byte)'e', (byte)'_', (byte)'f', (byte)'l', (byte)'a', (byte)'g', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogSdkVersion => // "sdk_version: "
            new[]
            {
                (byte)'s', (byte)'d', (byte)'k', (byte)'_', (byte)'v', (byte)'e', (byte)'r', (byte)'s',
                (byte)'i', (byte)'o', (byte)'n', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogSpec => // ", spec: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'s', (byte)'p', (byte)'e', (byte)'c', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogNx => // "NX"
            new[] { (byte)'N', (byte)'X' };

        public static ReadOnlySpan<byte> LogProgramIndex => // ", program_index: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'p', (byte)'r', (byte)'o', (byte)'g', (byte)'r', (byte)'a',
                (byte)'m', (byte)'_', (byte)'i', (byte)'n', (byte)'d', (byte)'e', (byte)'x', (byte)':',
                (byte)' '
            };

        public static ReadOnlySpan<byte> LogForSystem => // ", for_system: true"
            new[]
            {
                (byte)',', (byte)' ', (byte)'f', (byte)'o', (byte)'r', (byte)'_', (byte)'s', (byte)'y',
                (byte)'s', (byte)'t', (byte)'e', (byte)'m', (byte)':', (byte)' ', (byte)'t', (byte)'r',
                (byte)'u', (byte)'e'
            };

        public static ReadOnlySpan<byte> LogLineStart => // "FS_ACCESS: { "
            new[]
            {
                (byte)'F', (byte)'S', (byte)'_', (byte)'A', (byte)'C', (byte)'C', (byte)'E', (byte)'S',
                (byte)'S', (byte)':', (byte)' ', (byte)'{', (byte)' '
            };

        public static ReadOnlySpan<byte> LogLineEnd => // " }\n"
            new[] { (byte)' ', (byte)'}', (byte)'\n' };

        public static ReadOnlySpan<byte> LogStart => // "start: "
            new[]
            {
                (byte)'s', (byte)'t', (byte)'a', (byte)'r', (byte)'t', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogEnd => // ", end: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'e', (byte)'n', (byte)'d', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogResult => // ", result: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'r', (byte)'e', (byte)'s', (byte)'u', (byte)'l', (byte)'t',
                (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogHandle => // ", handle: 0x"
            new[]
            {
                (byte)',', (byte)' ', (byte)'h', (byte)'a', (byte)'n', (byte)'d', (byte)'l', (byte)'e',
                (byte)':', (byte)' ', (byte)'0', (byte)'x'
            };

        public static ReadOnlySpan<byte> LogPriority => // ", priority: "
            new[]
            {
                (byte)',', (byte)' ', (byte)'p', (byte)'r', (byte)'i', (byte)'o', (byte)'r', (byte)'i',
                (byte)'t', (byte)'y', (byte)':', (byte)' '
            };

        public static ReadOnlySpan<byte> LogFunction => // ", function: ""
            new[]
            {
                (byte)',', (byte)' ', (byte)'f', (byte)'u', (byte)'n', (byte)'c', (byte)'t', (byte)'i',
                (byte)'o', (byte)'n', (byte)':', (byte)' ', (byte)'"'
            };
    }
}
