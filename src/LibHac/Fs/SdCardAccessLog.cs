using System;
using LibHac.FsSrv;

namespace LibHac.Fs
{
    /// <summary>
    /// The default access logger that will output to the SD card via <see cref="FileSystemProxyImpl"/>.
    /// </summary>
    public class SdCardAccessLog : IAccessLog
    {
        public void Log(Result result, System.TimeSpan startTime, System.TimeSpan endTime, int handleId, string message, string caller = "")
        {
            throw new NotImplementedException();
        }
    }
}
