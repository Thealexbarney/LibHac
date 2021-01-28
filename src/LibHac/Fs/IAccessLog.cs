using System.Runtime.CompilerServices;

namespace LibHac.Fs
{
    public interface IAccessLog
    {
        void Log(Result result, System.TimeSpan startTime, System.TimeSpan endTime, int handleId, string message, [CallerMemberName] string caller = "");
    }
}