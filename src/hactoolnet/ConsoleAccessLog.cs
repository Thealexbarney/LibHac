using System;
using System.Runtime.CompilerServices;
using LibHac.Fs.Accessors;

namespace hactoolnet
{
    public class ConsoleAccessLog : IAccessLogger
    {
        public void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "")
        {
            Console.WriteLine(
                $"FS_ACCESS: {{ start: {startTime.Milliseconds,9}, end: {endTime.Milliseconds,9}, handle: 0x{handleId:x8}, function: \"{caller}\"{message} }}");
        }
    }
}
