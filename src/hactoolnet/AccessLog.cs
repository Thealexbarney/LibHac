using System;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac;
using LibHac.Fs;

namespace hactoolnet
{
    public class ConsoleAccessLog : IAccessLog
    {
        public void Log(Result result, System.TimeSpan startTime, System.TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "")
        {
            Console.WriteLine(AccessLogHelpers.BuildDefaultLogLine(result, startTime, endTime, handleId, message, caller));
        }
    }

    public class ProgressReportAccessLog : IAccessLog
    {
        private IProgressReport Logger { get; }
        public ProgressReportAccessLog(IProgressReport logger)
        {
            Logger = logger;
        }

        public void Log(Result result, System.TimeSpan startTime, System.TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "")
        {
            Logger.LogMessage(AccessLogHelpers.BuildDefaultLogLine(result, startTime, endTime, handleId, message, caller));
        }
    }

    public class TextWriterAccessLog : IAccessLog
    {
        private TextWriter Logger { get; }

        public TextWriterAccessLog(TextWriter logger)
        {
            Logger = logger;
        }

        public void Log(Result result, System.TimeSpan startTime, System.TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "")
        {
            Logger.WriteLine(AccessLogHelpers.BuildDefaultLogLine(result, startTime, endTime, handleId, message, caller));
        }
    }
}
