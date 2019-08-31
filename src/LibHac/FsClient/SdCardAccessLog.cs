using System;
using LibHac.FsService;

namespace LibHac.FsClient
{
    /// <summary>
    /// The default access logger that will output to the SD card via <see cref="FileSystemProxy"/>.
    /// </summary>
    public class SdCardAccessLog : IAccessLog
    {
        public void Log(TimeSpan startTime, TimeSpan endTime, int handleId, string message, string caller = "")
        {
            throw new NotImplementedException();
        }
    }
}
