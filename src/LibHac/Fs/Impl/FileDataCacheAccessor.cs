using System;
using LibHac.Fs.Fsa;

namespace LibHac.Fs.Impl
{
    internal class FileDataCacheAccessor
    {
        private IFileDataCache _cache;

        public FileDataCacheAccessor(IFileDataCache cache)
        {
            _cache = cache;
        }

        public Result Read(IFile file, out long bytesRead, long offset, Span<byte> destination, in ReadOption option,
            ref FileDataCacheAccessResult cacheAccessResult)
        {
            return _cache.Read(file, out bytesRead, offset, destination, in option, ref cacheAccessResult);
        }

        public void Purge(IFileSystem fileSystem)
        {
            _cache.Purge(fileSystem);
        }
    }
}
