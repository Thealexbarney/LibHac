using System;
using LibHac.Fs.Fsa;

namespace LibHac.Fs.Impl;

/// <summary>
/// Provides access to an <see cref="IFileDataCache"/>.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
internal class FileDataCacheAccessor
{
    private readonly IFileDataCache _cache;

    public FileDataCacheAccessor(IFileDataCache cache)
    {
        _cache = cache;
    }

    public Result Read(IFile file, out long bytesRead, long offset, Span<byte> destination, in ReadOption option,
        ref FileDataCacheAccessResult cacheAccessResult)
    {
        return _cache.Read(file, out bytesRead, offset, destination, in option, ref cacheAccessResult);
    }

    public void Purge(IStorage storage)
    {
        _cache.Purge(storage);
    }
}