using LibHac.Common.FixedArrays;

namespace LibHac.Fs;

public readonly struct CacheStorageListHandle
{
    internal readonly object Cache;

    internal CacheStorageListHandle(object cache)
    {
        Cache = cache;
    }
}

public struct CacheStorageInfo
{
    public int Index;
    public Array28<byte> Reserved;
}