using System;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Lr;
using LibHac.Ncm;

namespace LibHac.FsSrv.Impl
{
    internal class LocationResolverSet : IDisposable
    {
        private const int LocationResolverCount = 5;

        private LocationResolver[] _resolvers;
        private AddOnContentLocationResolver _aocResolver;
        private object _locker;

        private HorizonClient _hos;

        public LocationResolverSet(HorizonClient horizonClient)
        {
            _resolvers = new LocationResolver[LocationResolverCount];
            _locker = new object();
            _hos = horizonClient;
        }

        private Result GetLocationResolver(out LocationResolver resolver, StorageId storageId)
        {
            resolver = default;

            if (!IsValidStorageId(storageId))
                return ResultLr.LocationResolverNotFound.Log();

            lock (_locker)
            {
                int index = GetResolverIndexFromStorageId(storageId);
                ref LocationResolver lr = ref _resolvers[index];

                // Open the location resolver if it hasn't been already
                if (lr is null && _hos.Lr.OpenLocationResolver(out lr, storageId).IsFailure())
                    return ResultLr.LocationResolverNotFound.Log();

                resolver = lr;
                return Result.Success;
            }
        }

        private Result GetRegisteredLocationResolver(out RegisteredLocationResolver resolver)
        {
            Result rc = _hos.Lr.OpenRegisteredLocationResolver(out RegisteredLocationResolver lr);

            if (rc.IsFailure())
            {
                lr?.Dispose();
                resolver = default;
                return rc;
            }

            resolver = lr;
            return Result.Success;
        }

        private Result GetAddOnContentLocationResolver(out AddOnContentLocationResolver resolver)
        {
            lock (_locker)
            {
                if (_aocResolver is null)
                {
                    Result rc = _hos.Lr.OpenAddOnContentLocationResolver(out AddOnContentLocationResolver lr);
                    if (rc.IsFailure())
                    {
                        resolver = default;
                        return rc;
                    }

                    _aocResolver = lr;
                }

                resolver = _aocResolver;
                return Result.Success;
            }
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
            Assert.AssertTrue(IsValidStorageId(id));

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
