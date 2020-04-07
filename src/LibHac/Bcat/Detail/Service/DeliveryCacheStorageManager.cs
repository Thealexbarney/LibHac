namespace LibHac.Bcat.Detail.Service
{
    internal class DeliveryCacheStorageManager
    {
        private const int MaxEntryCount = 4;

        private readonly object _locker = new object();
        private Entry[] Entries { get; set; } = new Entry[MaxEntryCount];
        private bool UseRealStorage { get; set; }

        private struct Entry
        {
            public ulong ApplicationId { get; set; }
            public long RefCount { get; set; }
        }
    }
}
