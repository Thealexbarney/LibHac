using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using LibHac;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace hactoolnet
{
    internal class ResultLogger : Result.IResultLogger, IDisposable
    {
        private TextWriter Writer { get; }
        private bool PrintStackTrace { get; }
        private bool PrintSourceInfo { get; }
        private bool CombineRepeats { get; }

        private LogEntry _pendingEntry;
        private bool LastEntryPrintedNewLine { get; set; } = true;

        public ResultLogger(TextWriter writer, bool printStackTrace, bool printSourceInfo, bool combineRepeats)
        {
            Writer = writer ?? throw new ArgumentNullException(nameof(writer));
            PrintStackTrace = printStackTrace;
            PrintSourceInfo = printSourceInfo;
            CombineRepeats = combineRepeats;

            bool isDebugMode = false;
            CheckIfDebugMode(ref isDebugMode);

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (!isDebugMode)
            {
                Writer.WriteLine("The result log is only enabled when running in debug mode.");
            }
        }

        public void LogResult(Result result)
        {
            StackTrace st = GetStackTrace();
            MethodBase method = st.GetFrame(0)?.GetMethod();

            if (method is null)
                return;

            // This result from these functions is usually noise because they
            // are frequently used to detect if a file exists
            if (ResultFs.PathNotFound.Includes(result) &&
                typeof(IFileSystem).IsAssignableFrom(method.DeclaringType) &&
                method.Name.StartsWith(nameof(IFileSystem.GetEntryType)) ||
                method.Name.StartsWith(nameof(IAttributeFileSystem.GetFileAttributes)))
            {
                return;
            }

            AddLogEntry(new LogEntry(result, st));
        }

        public void LogConvertedResult(Result result, Result originalResult)
        {
            StackTrace st = GetStackTrace();

            AddLogEntry(new LogEntry(result, st, originalResult));
        }

        private void AddLogEntry(LogEntry entry)
        {
            if (CombineRepeats && _pendingEntry.IsRepeat(entry, PrintStackTrace && !entry.IsConvertedResult))
            {
                _pendingEntry.TimesCalled++;
                return;
            }

            PrintPendingEntry();

            if (CombineRepeats)
            {
                _pendingEntry = entry;
            }
            else
            {
                PrintLogEntry(entry);
            }
        }

        private void PrintPendingEntry()
        {
            if (_pendingEntry.StackTrace != null)
            {
                PrintLogEntry(_pendingEntry);
                _pendingEntry = default;
            }
        }

        private void PrintLogEntry(LogEntry entry)
        {
            MethodBase method = entry.StackTrace.GetFrame(0)?.GetMethod();

            if (method is null)
                return;

            string methodName = $"{method.DeclaringType?.FullName}.{method.Name}";

            bool printStackTrace = PrintStackTrace && !entry.IsConvertedResult;

            // Make sure there's a new line if printing a stack trace
            // A stack trace includes a new line at the end of it, so add the new line only if needed
            string entryText = printStackTrace && !LastEntryPrintedNewLine ? Environment.NewLine : string.Empty;

            string lineNumber = entry.LineNumber > 0 ? $":line{entry.LineNumber}" : string.Empty;

            if (entry.IsConvertedResult)
            {
                entryText += $"{entry.OriginalResult.ToStringWithName()} was converted to {entry.Result.ToStringWithName()} by {methodName}{lineNumber}";
            }
            else
            {
                entryText += $"{entry.Result.ToStringWithName()} was returned by {methodName}{lineNumber}";
            }

            if (entry.TimesCalled > 1)
            {
                entryText += $" {entry.TimesCalled} times";
            }

            Writer.WriteLine(entryText);

            if (printStackTrace)
            {
                Writer.WriteLine(entry.StackTraceText);
            }

            LastEntryPrintedNewLine = printStackTrace;
        }

        // Returns the stack trace starting at the method that called Log()
        private StackTrace GetStackTrace()
        {
            var st = new StackTrace();
            int framesToSkip = 0;

            for (; framesToSkip < st.FrameCount; framesToSkip++)
            {
                Type declaringType = st.GetFrame(framesToSkip)?.GetMethod()?.DeclaringType;

                if (declaringType == null)
                {
                    framesToSkip = 0;
                    break;
                }

                if (declaringType != typeof(ResultLogger) &&
                    declaringType != typeof(Result) &&
                    declaringType != typeof(Result.Base))
                {
                    break;
                }
            }

            return new StackTrace(framesToSkip, PrintSourceInfo);
        }

        // You can't negate a conditional attribute, so this is a hacky workaround
        [Conditional("DEBUG")]
        // ReSharper disable once RedundantAssignment
        private void CheckIfDebugMode(ref bool isDebugMode)
        {
            isDebugMode = true;
        }

        public void Dispose()
        {
            PrintPendingEntry();
            Writer.Dispose();
        }

        private struct LogEntry
        {
            public Result Result { get; }
            public Result OriginalResult { get; }
            public string CallingMethod { get; }
            public StackTrace StackTrace { get; }
            public string StackTraceText { get; }

            // Line number will be 0 if there's no source info
            public int LineNumber { get; }
            public int TimesCalled { get; set; }
            public bool IsConvertedResult { get; }

            public LogEntry(Result result, StackTrace stackTrace) : this(result, stackTrace, false, default) { }
            public LogEntry(Result result, StackTrace stackTrace, Result originalResult) : this(result, stackTrace, true, originalResult) { }

            private LogEntry(Result result, StackTrace stackTrace, bool isConverted, Result originalResult)
            {
                Result = result;
                StackTrace = stackTrace;
                IsConvertedResult = isConverted;
                OriginalResult = originalResult;

                MethodBase method = stackTrace.GetFrame(0)?.GetMethod();

                if (method is null)
                {
                    CallingMethod = string.Empty;
                    StackTraceText = string.Empty;
                    LineNumber = 0;
                    TimesCalled = 1;

                    return;
                }

                CallingMethod = $"{method.DeclaringType?.FullName}.{method.Name}";

                StackTraceText = stackTrace.ToString();
                LineNumber = stackTrace.GetFrame(0)?.GetFileLineNumber() ?? 0;
                TimesCalled = 1;
            }

            public bool IsRepeat(LogEntry other, bool compareByStackTrace)
            {
                if (Result != other.Result ||
                    IsConvertedResult != other.IsConvertedResult ||
                    (IsConvertedResult && OriginalResult != other.OriginalResult))
                {
                    return false;
                }

                if (compareByStackTrace)
                {
                    return StackTraceText == other.StackTraceText;
                }

                return LineNumber == other.LineNumber && CallingMethod == other.CallingMethod;
            }
        }
    }
}
