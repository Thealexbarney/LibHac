using System;
using System.Runtime.CompilerServices;

namespace LibHac.FsClient
{
    public interface IAccessLog
    {
        void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "");
    }
}