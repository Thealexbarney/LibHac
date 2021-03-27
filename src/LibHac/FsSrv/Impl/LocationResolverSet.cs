using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Lr;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.FsSrv.Impl
{
    internal class LocationResolverSet : IDisposable
    {
        private const int LocationResolverCount = 5;

        private LocationResolver[] _resolvers;
        private AddOnContentLocationResolver _aocResolver;
        private SdkMutexType _mutex;

        private FileSystemServer _fsServer;
        private HorizonClient Hos => _fsServer.Hos;

        public LocationResolverSet(FileSystemServer fsServer)
        {
            _resolvers = new LocationResolver[LocationResolverCount];
            _mutex.Initialize();
            _fsServer = fsServer;
        }

        private Result GetLocationResolver(out LocationResolver resolver, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out resolver);

            if (!IsValidStorageId(storageId))
                return ResultLr.LocationResolverNotFound.Log();

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            int index = GetResolverIndexFromStorageId(storageId);
            ref LocationResolver lr = ref _resolvers[index];

            // Open the location resolver if it hasn't been already
            if (lr is null && Hos.Lr.OpenLocationResolver(out lr, storageId).IsFailure())
                return ResultLr.LocationResolverNotFound.Log();

            resolver = lr;
            return Result.Success;
        }

        private Result GetRegisteredLocationResolver(out RegisteredLocationResolver resolver)
        {
            Result rc = Hos.Lr.OpenRegisteredLocationResolver(out RegisteredLocationResolver lr);

            if (rc.IsFailure())
            {
                lr?.Dispose();
                UnsafeHelpers.SkipParamInit(out resolver);
                return rc;
            }

            resolver = lr;
            return Result.Success;
        }

        private Result GetAddOnContentLocationResolver(out AddOnContentLocationResolver resolver)
        {
            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            if (_aocResolver is null)
            {
                Result rc = Hos.Lr.OpenAddOnContentLocationResolver(out AddOnContentLocationResolver lr);
                if (rc.IsFailure())
                {
                    UnsafeHelpers.SkipParamInit(out resolver);
                    return rc;
                }

                _aocResolver = lr;
            }

            resolver = _aocResolver;
            return Result.Success;
        }

        public Result ResolveApplicationControlPath(out Path path, Ncm.ApplicationId applicationId, StorageId storageId)
        {
            Path.InitEmpty(out path);

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveApplicationControlPath(out path, applicationId);
            if (rc.IsFailure()) return rc;

            PathUtility.Replace(path.StrMutable, (byte)'\\', (byte)'/');
            return Result.Success;
        }

        public Result ResolveApplicationHtmlDocumentPath(out Path path, Ncm.ApplicationId applicationId, StorageId storageId)
        {
            Path.InitEmpty(out path);

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveApplicationHtmlDocumentPath(out path, applicationId);
            if (rc.IsFailure()) return rc;

            PathUtility.Replace(path.StrMutable, (byte)'\\', (byte)'/');
            return Result.Success;
        }

        public virtual Result ResolveProgramPath(out Path path, ulong id, StorageId storageId)
        {
            Path.InitEmpty(out path);

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveProgramPath(out path, new ProgramId(id));
            if (rc.IsFailure()) return rc;

            PathUtility.Replace(path.StrMutable, (byte)'\\', (byte)'/');
            return Result.Success;
        }

        public Result ResolveRomPath(out Path path, ulong id, StorageId storageId)
        {
            Path.InitEmpty(out path);

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveProgramPath(out path, new ProgramId(id));
            if (rc.IsFailure()) return rc;

            PathUtility.Replace(path.StrMutable, (byte)'\\', (byte)'/');
            return Result.Success;
        }

        public Result ResolveAddOnContentPath(out Path path, DataId dataId)
        {
            Path.InitEmpty(out path);

            Result rc = GetAddOnContentLocationResolver(out AddOnContentLocationResolver resolver);
            if (rc.IsFailure()) return rc;

            return resolver.ResolveAddOnContentPath(out path, dataId);
        }

        public Result ResolveDataPath(out Path path, DataId dataId, StorageId storageId)
        {
            Path.InitEmpty(out path);

            if (storageId == StorageId.None)
                return ResultFs.InvalidAlignment.Log();

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            return resolver.ResolveDataPath(out path, dataId);
        }

        public Result ResolveRegisteredProgramPath(out Path path, ulong id)
        {
            Path.InitEmpty(out path);

            Result rc = GetRegisteredLocationResolver(out RegisteredLocationResolver resolver);
            if (rc.IsFailure()) return rc;

            using (resolver)
            {
                return resolver.ResolveProgramPath(out path, new ProgramId(id));
            }
        }

        public Result ResolveRegisteredHtmlDocumentPath(out Path path, ulong id)
        {
            Path.InitEmpty(out path);

            Result rc = GetRegisteredLocationResolver(out RegisteredLocationResolver resolver);
            if (rc.IsFailure()) return rc;

            using (resolver)
            {
                return resolver.ResolveHtmlDocumentPath(out path, new ProgramId(id));
            }
        }

        private static bool IsValidStorageId(StorageId id)
        {
            return id == StorageId.Host ||
                   id == StorageId.GameCard ||
                   id == StorageId.BuiltInSystem ||
                   id == StorageId.BuiltInUser ||
                   id == StorageId.SdCard;
        }

        private static int GetResolverIndexFromStorageId(StorageId id)
        {
            Assert.SdkRequires(IsValidStorageId(id));

            return id switch
            {
                StorageId.Host => 2,
                StorageId.GameCard => 4,
                StorageId.BuiltInSystem => 0,
                StorageId.BuiltInUser => 1,
                StorageId.SdCard => 3,
                _ => -1
            };
        }

        public void Dispose()
        {
            foreach (LocationResolver resolver in _resolvers)
            {
                resolver?.Dispose();
            }

            _aocResolver?.Dispose();
        }
    }
}
