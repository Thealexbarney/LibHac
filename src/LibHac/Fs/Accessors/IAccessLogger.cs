using System;
using System.Runtime.CompilerServices;

namespace LibHac.Fs.Accessors
{
    public interface IAccessLogger
    {
        void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "");
    }
}