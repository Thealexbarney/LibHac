namespace LibHac.Fs
{
    public enum Priority
    {
        Realtime = 0,
        Normal = 1,
        Low = 2
    }

    public enum PriorityRaw
    {
        Realtime = 0,
        Normal = 1,
        Low = 2,
        Background = 3
    }
}

namespace LibHac.Fs.Shim
{
    public static class PriorityShim
    {
        public static void SetPriorityOnCurrentThread(this FileSystemClient fs, Priority priority)
        {
            // Todo
        }

        public static Priority GetPriorityOnCurrentThread(this FileSystemClient fs)
        {
            // Todo
            return Priority.Normal;
        }

        public static void SetPriorityRawOnCurrentThread(this FileSystemClient fs, PriorityRaw priority)
        {
            // Todo
        }

        public static PriorityRaw GetPriorityRawOnCurrentThreadForInternalUse(this FileSystemClient fs)
        {
            // Todo
            return PriorityRaw.Normal;
        }

        public static PriorityRaw GetPriorityRawOnCurrentThread(this FileSystemClient fs)
        {
            // Todo
            return PriorityRaw.Normal;
        }
    }
}
