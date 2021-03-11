using System;
using LibHac.Common;

namespace LibHac.Diag
{
    public enum LogSeverity
    {
        Trace,
        Info,
        Warn,
        Error,
        Fatal
    }

    public ref struct LogMetaData
    {
        public SourceInfo SourceInfo;
        public U8Span ModuleName;
        public LogSeverity Severity;
        public int Verbosity;
        public bool UseDefaultLocaleCharset;
        public Span<byte> AdditionalData;
    }

    public struct SourceInfo
    {
        public int LineNumber;
        public string FileName;
        public string FunctionName;
    }

    public ref struct LogBody
    {
        public U8Span Message;
        public bool IsHead;
        public bool IsTail;
    }
}
