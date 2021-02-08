using LibHac.Os.Impl;

namespace LibHac.Os
{
    public static class Thread
    {
        public static void SleepThread(this OsState os, TimeSpan time)
        {
            TimeoutHelperImpl.Sleep(os, time);
        }
    }
}
