namespace LibHac.Fs
{
    public static class AccessLogHelpers
    {
        public static string BuildDefaultLogLine(Result result, System.TimeSpan startTime, System.TimeSpan endTime, int handleId,
            string message, string caller)
        {
            return
                $"FS_ACCESS: {{ start: {(long)startTime.TotalMilliseconds,9}, end: {(long)endTime.TotalMilliseconds,9}, result: 0x{result.Value:x8}, handle: 0x{handleId:x8}, function: \"{caller}\"{message} }}";
        }
    }
}
