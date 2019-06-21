﻿using System;
using System.IO;
using System.Runtime.CompilerServices;
using LibHac;
using LibHac.Fs.Accessors;

namespace hactoolnet
{
    public class ConsoleAccessLog : IAccessLog
    {
        public void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "")
        {
            Console.WriteLine(CommonAccessLog.BuildLogLine(startTime, endTime, handleId, message, caller));
        }
    }

    public class ProgressReportAccessLog : IAccessLog
    {
        private IProgressReport Logger { get; }
        public ProgressReportAccessLog(IProgressReport logger)
        {
            Logger = logger;
        }

        public void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "")
        {
            Logger.LogMessage(CommonAccessLog.BuildLogLine(startTime, endTime, handleId, message, caller));
        }
    }

    public class TextWriterAccessLog : IAccessLog
    {
        private TextWriter Logger { get; }

        public TextWriterAccessLog(TextWriter logger)
        {
            Logger = logger;
        }

        public void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "")
        {
            Logger.WriteLine(CommonAccessLog.BuildLogLine(startTime, endTime, handleId, message, caller));
        }
    }

    public static class CommonAccessLog
    {
        public static string BuildLogLine(TimeSpan startTime, TimeSpan endTime, int handleId, string message,
            string caller)
        {
            return $"FS_ACCESS: {{ start: {(long)startTime.TotalMilliseconds,9}, end: {(long)endTime.TotalMilliseconds,9}, handle: 0x{handleId:x8}, function: \"{caller}\"{message} }}";
        }
    }
}
