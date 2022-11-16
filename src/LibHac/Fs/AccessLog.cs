using System;
using System.Buffers;
using System.Buffers.Text;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Impl;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Sf;
using LibHac.Gc.Impl;
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

    public struct ApplicationInfo
    {
        public Ncm.ApplicationId ApplicationId;
        public uint Version;
        public byte LaunchType;
        public bool IsMultiProgram;
        public Array18<byte> Reserved;
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

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.GetGlobalAccessLogMode(out mode);
            fs.Impl.AbortIfNeeded(res);
            return res;
        }

        public static Result SetGlobalAccessLogMode(this FileSystemClient fs, GlobalAccessLogMode mode)
        {
            // Allow the access log to be used without an FS server by storing the mode locally in that situation.
            if (fs.Globals.AccessLog.IsServerless)
            {
                fs.Globals.AccessLog.GlobalAccessLogMode = mode;
                return Result.Success;
            }

            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

            Result res = fileSystemProxy.Get.SetGlobalAccessLogMode(mode);
            fs.Impl.AbortIfNeeded(res);
            return res;
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
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();
            fileSystemProxy.Get.OutputApplicationInfoAccessLog(in applicationInfo).IgnoreResult();
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

    public struct IdString
    {
        private Buffer32 _buffer;

        [UnscopedRef]
        [MethodImpl(MethodImplOptions.NoInlining)]
        private ReadOnlySpan<byte> ToValueString(int value)
        {
            bool success = Utf8Formatter.TryFormat(value, _buffer.Bytes, out int length);
            Assert.SdkAssert(success);
            Assert.SdkLess(length, _buffer.Bytes.Length);
            _buffer[length] = 0;

            return _buffer.Bytes;
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(Priority value)
        {
            switch (value)
            {
                case Priority.Realtime: return "Realtime"u8;
                case Priority.Normal: return "Normal"u8;
                case Priority.Low: return "Low"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(PriorityRaw value)
        {
            switch (value)
            {
                case PriorityRaw.Realtime: return "Realtime"u8;
                case PriorityRaw.Normal: return "Normal"u8;
                case PriorityRaw.Low: return "Low"u8;
                case PriorityRaw.Background: return "Background"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(ImageDirectoryId value)
        {
            switch (value)
            {
                case ImageDirectoryId.Nand: return "Nand"u8;
                case ImageDirectoryId.SdCard: return "SdCard"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(ContentStorageId value)
        {
            switch (value)
            {
                case ContentStorageId.System: return "System"u8;
                case ContentStorageId.User: return "User"u8;
                case ContentStorageId.SdCard: return "SdCard"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(GameCardPartition value)
        {
            switch (value)
            {
                case GameCardPartition.Update: return "Update"u8;
                case GameCardPartition.Normal: return "Normal"u8;
                case GameCardPartition.Secure: return "Secure"u8;
                case GameCardPartition.Logo: return "Logo"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(SaveDataSpaceId value)
        {
            switch (value)
            {
                case SaveDataSpaceId.System: return "System"u8;
                case SaveDataSpaceId.User: return "User"u8;
                case SaveDataSpaceId.SdSystem: return "SdSystem"u8;
                case SaveDataSpaceId.ProperSystem: return "ProperSystem"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(SaveDataFormatType value)
        {
            switch (value)
            {
                case SaveDataFormatType.Normal: return "Normal"u8;
                case SaveDataFormatType.NoJournal: return "NoJournal"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(ContentType value)
        {
            switch (value)
            {
                case ContentType.Meta: return "Meta"u8;
                case ContentType.Control: return "Control"u8;
                case ContentType.Manual: return "Manual"u8;
                case ContentType.Logo: return "Logo"u8;
                case ContentType.Data: return "Data"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(BisPartitionId value)
        {
            switch (value)
            {
                case BisPartitionId.BootPartition1Root: return "BootPartition1Root"u8;
                case BisPartitionId.BootPartition2Root: return "BootPartition2Root"u8;
                case BisPartitionId.UserDataRoot: return "UserDataRoot"u8;
                case BisPartitionId.BootConfigAndPackage2Part1: return "BootConfigAndPackage2Part1"u8;
                case BisPartitionId.BootConfigAndPackage2Part2: return "BootConfigAndPackage2Part2"u8;
                case BisPartitionId.BootConfigAndPackage2Part3: return "BootConfigAndPackage2Part3"u8;
                case BisPartitionId.BootConfigAndPackage2Part4: return "BootConfigAndPackage2Part4"u8;
                case BisPartitionId.BootConfigAndPackage2Part5: return "BootConfigAndPackage2Part5"u8;
                case BisPartitionId.BootConfigAndPackage2Part6: return "BootConfigAndPackage2Part6"u8;
                case BisPartitionId.CalibrationBinary: return "CalibrationBinary"u8;
                case BisPartitionId.CalibrationFile: return "CalibrationFile"u8;
                case BisPartitionId.SafeMode: return "SafeMode"u8;
                case BisPartitionId.User: return "User"u8;
                case BisPartitionId.System: return "System"u8;
                case BisPartitionId.SystemProperEncryption: return "SystemProperEncryption"u8;
                case BisPartitionId.SystemProperPartition: return "SystemProperPartition"u8;
                case (BisPartitionId)35: return "Invalid"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(DirectoryEntryType value)
        {
            switch (value)
            {
                case DirectoryEntryType.Directory: return "Directory"u8;
                case DirectoryEntryType.File: return "File"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(MountHostOption value)
        {
            switch (value.Flags)
            {
                case MountHostOptionFlag.PseudoCaseSensitive: return "MountHostOptionFlag_PseudoCaseSensitive"u8;
                default: return ToValueString((int)value.Flags);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(MemoryCapacity value)
        {
            switch (value)
            {
                case MemoryCapacity.Capacity1GB: return "1GB"u8;
                case MemoryCapacity.Capacity2GB: return "2GB"u8;
                case MemoryCapacity.Capacity4GB: return "4GB"u8;
                case MemoryCapacity.Capacity8GB: return "8GB"u8;
                case MemoryCapacity.Capacity16GB: return "16GB"u8;
                case MemoryCapacity.Capacity32GB: return "32GB"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(SelSec value)
        {
            switch (value)
            {
                case SelSec.T1: return "T1"u8;
                case SelSec.T2: return "T2"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(KekIndex value)
        {
            switch (value)
            {
                case KekIndex.Version0: return "Version0"u8;
                case KekIndex.ForDev: return "VersionForDev"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(AccessControl1ClockRate value)
        {
            switch (value)
            {
                case AccessControl1ClockRate.ClockRate25MHz: return "25 MHz"u8;
                case AccessControl1ClockRate.ClockRate50MHz: return "50 MHz"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(FwVersion value)
        {
            switch (value)
            {
                case FwVersion.ForDev: return "ForDev"u8;
                case FwVersion.Since1_0_0: return "1.0.0"u8;
                case FwVersion.Since4_0_0: return "4.0.0"u8;
                case FwVersion.Since9_0_0: return "9.0.0"u8;
                case FwVersion.Since11_0_0: return "11.0.0"u8;
                case FwVersion.Since12_0_0: return "12.0.0"u8;
                default: return ToValueString((int)value);
            }
        }

        [UnscopedRef]
        public ReadOnlySpan<byte> ToString(GameCardCompatibilityType value)
        {
            switch (value)
            {
                case GameCardCompatibilityType.Normal: return "Normal"u8;
                case GameCardCompatibilityType.Terra: return "Terra"u8;
                default: return ToValueString((int)value);
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
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            fileSystemProxy.Get.FlushAccessLogOnSdCard().IgnoreResult();
        }

        private static void OutputAccessLogToSdCardImpl(FileSystemClientImpl fs, U8Span message)
        {
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            fileSystemProxy.Get.OutputAccessLogToSdCard(new InBuffer(message.Value)).IgnoreResult();
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
            using SharedRef<IFileSystemProxy> fileSystemProxy = fs.GetFileSystemProxyServiceObject();
            Result res = fileSystemProxy.Get.GetProgramIndexForAccessLog(out index, out count);
            Abort.DoAbortUnless(res.IsSuccess());
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
                    Result res = fs.Fs.GetGlobalAccessLogMode(out g.GlobalAccessLogMode);
                    fs.LogResultErrorMessage(res);
                    if (res.IsFailure()) Abort.DoAbort(res);

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
            Result res = fs.Find(out FileSystemAccessor accessor, mountName);

            if (res.IsFailure())
                return true;

            return accessor.IsEnabledAccessLog();
        }

        public static void EnableFileSystemAccessorAccessLog(this FileSystemClientImpl fs, U8Span mountName)
        {
            Result res = fs.Find(out FileSystemAccessor fileSystem, mountName);
            fs.LogResultErrorMessage(res);
            Abort.DoAbortUnless(res.IsSuccess());

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
        /// <summary>"<c>$fs</c>"</summary>
        public static ReadOnlySpan<byte> FsModuleName => "$fs"u8;

        /// <summary>"<c>0.17.0</c>"</summary>
        public static ReadOnlySpan<byte> LogLibHacVersion => "0.17.0"u8;

        /// <summary>"<c>"</c>"</summary>
        public static byte LogQuote => (byte)'"';

        /// <summary>"<c>true</c>"</summary>
        public static ReadOnlySpan<byte> LogTrue => "true"u8;

        /// <summary>"<c>false</c>"</summary>
        public static ReadOnlySpan<byte> LogFalse => "false"u8;

        /// <summary>"<c>, entry_buffer_count: </c>"</summary>
        public static ReadOnlySpan<byte> LogEntryBufferCount => ", entry_buffer_count: "u8;

        /// <summary>"<c>, entry_count: </c>"</summary>
        public static ReadOnlySpan<byte> LogEntryCount => ", entry_count: "u8;

        /// <summary>"<c>, offset: </c>"</summary>
        public static ReadOnlySpan<byte> LogOffset => ", offset: "u8;

        /// <summary>"<c>, size: </c>"</summary>
        public static ReadOnlySpan<byte> LogSize => ", size: "u8;

        /// <summary>"<c>, read_size: </c>"</summary>
        public static ReadOnlySpan<byte> LogReadSize => ", read_size: "u8;

        /// <summary>"<c>, write_option: Flush</c>"</summary>
        public static ReadOnlySpan<byte> LogWriteOptionFlush => ", write_option: Flush"u8;

        /// <summary>"<c>, open_mode: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogOpenMode => ", open_mode: 0x"u8;

        /// <summary>"<c>, path: "</c>"</summary>
        public static ReadOnlySpan<byte> LogPath => ", path: \""u8;

        /// <summary>"<c>", new_path: "</c>"</summary>
        public static ReadOnlySpan<byte> LogNewPath => "\", new_path: \""u8;

        /// <summary>"<c>", entry_type: </c>"</summary>
        public static ReadOnlySpan<byte> LogEntryType => "\", entry_type: "u8;

        /// <summary>"<c>, name: "</c>"</summary>
        public static ReadOnlySpan<byte> LogName => ", name: \""u8;

        /// <summary>"<c>", commit_option: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogCommitOption => "\", commit_option: 0x"u8;

        /// <summary>"<c>", is_mounted: "</c>"</summary>
        public static ReadOnlySpan<byte> LogIsMounted => "\", is_mounted: \""u8;

        /// <summary>"<c>, applicationid: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogApplicationId => ", applicationid: 0x"u8;

        /// <summary>"<c>, programid: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogProgramId => ", programid: 0x"u8;

        /// <summary>"<c>, dataid: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogDataId => ", dataid: 0x"u8;

        /// <summary>"<c>, bispartitionid: </c>"</summary>
        public static ReadOnlySpan<byte> LogBisPartitionId => ", bispartitionid: "u8;

        /// <summary>"<c>, content_type: </c>"</summary>
        public static ReadOnlySpan<byte> LogContentType => ", content_type: "u8;

        /// <summary>"<c>, contentstorageid: </c>"</summary>
        public static ReadOnlySpan<byte> LogContentStorageId => ", contentstorageid: "u8;

        /// <summary>"<c>, imagedirectoryid: </c>"</summary>
        public static ReadOnlySpan<byte> LogImageDirectoryId => ", imagedirectoryid: "u8;

        /// <summary>"<c>, gamecard_handle: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogGameCardHandle => ", gamecard_handle: 0x"u8;

        /// <summary>"<c>, gamecard_partition: </c>"</summary>
        public static ReadOnlySpan<byte> LogGameCardPartition => ", gamecard_partition: "u8;

        /// <summary>"<c>, mount_host_option: </c>"</summary>
        public static ReadOnlySpan<byte> LogMountHostOption => ", mount_host_option: "u8;

        /// <summary>"<c>, root_path: "</c>"</summary>
        public static ReadOnlySpan<byte> LogRootPath => ", root_path: \""u8;

        /// <summary>"<c>, userid: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogUserId => ", userid: 0x"u8;

        /// <summary>"<c>, index: </c>"</summary>
        public static ReadOnlySpan<byte> LogIndex => ", index: "u8;

        /// <summary>"<c>, save_data_owner_id: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataOwnerId => ", save_data_owner_id: 0x"u8;

        /// <summary>"<c>, save_data_size: </c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataSize => ", save_data_size: "u8;

        /// <summary>"<c>, save_data_journal_size: </c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataJournalSize => ", save_data_journal_size: "u8;

        /// <summary>"<c>, save_data_flags: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataFlags => ", save_data_flags: 0x"u8;

        /// <summary>"<c>, savedataid: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataId => ", savedataid: 0x"u8;

        /// <summary>"<c>, savedataspaceid: </c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataSpaceId => ", savedataspaceid: "u8;

        /// <summary>"<c>, save_data_format_type: </c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataFormatType => ", save_data_format_type: "u8;

        /// <summary>"<c>, save_data_time_stamp: </c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataTimeStamp => ", save_data_time_stamp: "u8;

        /// <summary>"<c>, save_data_commit_id: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogSaveDataCommitId => ", save_data_commit_id: 0x"u8;

        /// <summary>"<c>, restore_flag: </c>"</summary>
        public static ReadOnlySpan<byte> LogRestoreFlag => ", restore_flag: "u8;

        /// <summary>"<c>sdk_version: </c>"</summary>
        public static ReadOnlySpan<byte> LogSdkVersion => "sdk_version: "u8;

        /// <summary>"<c>, spec: </c>"</summary>
        public static ReadOnlySpan<byte> LogSpec => ", spec: "u8;

        /// <summary>"<c>NX</c>"</summary>
        public static ReadOnlySpan<byte> LogNx => "NX"u8;

        /// <summary>"<c>, program_index: </c>"</summary>
        public static ReadOnlySpan<byte> LogProgramIndex => ", program_index: "u8;

        /// <summary>"<c>, for_system: true</c>"</summary>
        public static ReadOnlySpan<byte> LogForSystem => ", for_system: true"u8;

        /// <summary>"<c>"FS_ACCESS: { </c>"</summary>
        public static ReadOnlySpan<byte> LogLineStart => "FS_ACCESS: { "u8;

        /// <summary>"<c> }\n</c>"</summary>
        public static ReadOnlySpan<byte> LogLineEnd => " }\n"u8;

        /// <summary>"<c>start: </c>"</summary>
        public static ReadOnlySpan<byte> LogStart => "start: "u8;

        /// <summary>"<c>, end: </c>"</summary>
        public static ReadOnlySpan<byte> LogEnd => ", end: "u8;

        /// <summary>"<c>, result: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogResult => ", result: 0x"u8;

        /// <summary>"<c>, handle: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogHandle => ", handle: 0x"u8;

        /// <summary>"<c>, priority: </c>"</summary>
        public static ReadOnlySpan<byte> LogPriority => ", priority: "u8;

        /// <summary>"<c>, function: "</c>"</summary>
        public static ReadOnlySpan<byte> LogFunction => ", function: \""u8;

        /// <summary>"<c>, cachestoragelist_handle: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogCacheStorageListHandle => ", cachestoragelist_handle: 0x"u8;

        /// <summary>"<c>, infobuffercount: 0x</c>"</summary>
        public static ReadOnlySpan<byte> LogInfoBufferCount => ", infobuffercount: 0x</c>"u8;

        /// <summary>"<c>, cache_storage_count: </c>"</summary>
        public static ReadOnlySpan<byte> LogCacheStorageCount => ", cache_storage_count: "u8;
    }
}