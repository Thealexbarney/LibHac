using System;

namespace LibHac.Os.Impl
{
    internal class OsResourceManager : IDisposable
    {
        public TickManager TickManager { get; }

        // Todo: Use configuration object if/when more options are added
        public OsResourceManager(long startTick)
        {
            TickManager = new TickManager(startTick);
        }

        public void Dispose()
        {
            TickManager.Dispose();
        }
    }

    internal static class OsResourceManagerApi
    {
        public static OsResourceManager GetOsResourceManager(this OsState os)
        {
            return os.ResourceManager;
        }
    }
}
