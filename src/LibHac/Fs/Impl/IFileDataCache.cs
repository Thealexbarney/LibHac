using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs.Fsa;

namespace LibHac.Fs.Impl
{
    // ReSharper disable once InconsistentNaming
    internal abstract class IFileDataCache : IDisposable
    {
        public abstract void Dispose();

        public abstract void Purge(IFileSystem fileSystem);

        protected abstract Result DoRead(IFile file, out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option, ref FileDataCacheAccessResult cacheAccessResult);

        public Result Read(IFile file, out long bytesRead, long offset, Span<byte> destination, in ReadOption option,
            ref FileDataCacheAccessResult cacheAccessResult)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            if (destination.Length == 0)
            {
                bytesRead = 0;
                cacheAccessResult.SetFileDataCacheUsed(true);
                return Result.Success;
            }

            if (offset < 0)
                return ResultFs.OutOfRange.Log();

            if (destination.Length < 0)
                return ResultFs.OutOfRange.Log();

            if (long.MaxValue - offset < destination.Length)
                return ResultFs.OutOfRange.Log();

            return DoRead(file, out bytesRead, offset, destination, in option, ref cacheAccessResult);
        }
    }

    internal struct FileDataCacheAccessResult
    {
        private const int MaxRegionCount = 8;

        private int _regionCount;
        private Array8<FileRegion> _regions;
        private bool _isFileDataCacheUsed;
        private bool _exceededMaxRegionCount;

        public bool IsFileDataCacheUsed() => _isFileDataCacheUsed;
        public bool SetFileDataCacheUsed(bool useFileDataCache) => _isFileDataCacheUsed = useFileDataCache;

        public int GetCacheFetchedRegionCount()
        {
            Assert.SdkRequires(_isFileDataCacheUsed);
            return _regionCount;
        }

        public bool ExceededMaxCacheFetchedRegionCount() => _exceededMaxRegionCount;

        public FileRegion GetCacheFetchedRegion(int index)
        {
            Assert.SdkRequires(IsFileDataCacheUsed());
            Assert.SdkRequiresLessEqual(0, index);
            Assert.SdkRequiresLess(index, _regionCount);

            return _regions[index];
        }

        public void AddCacheFetchedRegion(FileRegion region)
        {
            _isFileDataCacheUsed = true;

            if (region.Size == 0)
                return;

            if (_regionCount >= MaxRegionCount)
            {
                _regions[MaxRegionCount - 1] = region;
                _exceededMaxRegionCount = true;
            }
            else
            {
                _regions[_regionCount] = region;
                _regionCount++;
            }
        }
    }
}
