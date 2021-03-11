using System;
using System.Runtime.CompilerServices;
using LibHac.Common;

namespace LibHac.Diag
{
    public static class Log
    {
        // Todo: Should we split large logs into smaller chunks like Horizon does?
        public static void LogImpl(this DiagClientImpl diag, in LogMetaData metaData, ReadOnlySpan<byte> message)
        {
            diag.PutImpl(in metaData, message);
        }

        public static void PutImpl(this DiagClientImpl diag, in LogMetaData metaData, ReadOnlySpan<byte> message)
        {
            var logBody = new LogBody
            {
                Message = new U8Span(message),
                IsHead = true,
                IsTail = true
            };

            diag.CallAllLogObserver(in metaData, in logBody);
        }

        public static void LogImpl(this DiagClientImpl diag, ReadOnlySpan<byte> moduleName, LogSeverity severity,
            ReadOnlySpan<byte> message, [CallerLineNumber] int lineNumber = 0, [CallerFilePath] string fileName = "",
            [CallerMemberName] string functionName = "")
        {
            var metaData = new LogMetaData
            {
                SourceInfo = new SourceInfo
                {
                    LineNumber = lineNumber,
                    FileName = fileName,
                    FunctionName = functionName
                },
                ModuleName = new U8Span(moduleName),
                Severity = severity,
                Verbosity = 0
            };

            diag.LogImpl(in metaData, message);
        }
    }
}
