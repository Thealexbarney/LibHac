using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Lr;
using LibHac.Ncm;
using LibHac.Os;

namespace LibHac.FsSrv.Impl
{
    public static class LocationResolverSetGlobalMethods
    {
        public static void InitializeLocationResolverSet(this FileSystemServer fsSrv)
        {
            ref LocationResolverSetGlobals globals = ref fsSrv.Globals.LocationResolverSet;
            using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref globals.Mutex);

            if (!globals.IsLrInitialized)
            {
                fsSrv.Hos.Lr.Initialize();
                globals.IsLrInitialized = true;
            }
        }
    }

    internal struct LocationResolverSetGlobals
    {
        public SdkMutexType Mutex;
        public bool IsLrInitialized;

        public void Initialize()
        {
            Mutex.Initialize();
        }
    }

    /// <summary>
    /// Manages resolving the location of NCAs via the <c>lr</c> service.
    /// </summary>
    /// <remarks>Based on FS 12.0.3 (nnSdk 12.3.1)</remarks>
    internal class LocationResolverSet : IDisposable
    {
        private const int LocationResolverCount = 5;

        // Todo: Use Optional<T>
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

        public void Dispose()
        {
            foreach (LocationResolver resolver in _resolvers)
            {
                resolver?.Dispose();
            }

            _aocResolver?.Dispose();
        }

        private static Result SetUpFsPath(ref Fs.Path outPath, in Lr.Path lrPath)
        {
            var pathFlags = new PathFlags();
            pathFlags.AllowMountName();

            if (Utility.IsHostFsMountName(lrPath.Str))
                pathFlags.AllowWindowsPath();

            Result rc = outPath.InitializeWithReplaceUnc(lrPath.Str);
            if (rc.IsFailure()) return rc;

            rc = outPath.Normalize(pathFlags);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private Result GetLocationResolver(out LocationResolver resolver, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out resolver);

            _fsServer.InitializeLocationResolverSet();

            if (!IsValidStorageId(storageId))
                return ResultLr.LocationResolverNotFound.Log();

            using ScopedLock<SdkMutexType> lk = ScopedLock.Lock(ref _mutex);

            int index = GetResolverIndexFromStorageId(storageId);

            if (index < 0)
                return ResultLr.LocationResolverNotFound.Log();

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
            _fsServer.InitializeLocationResolverSet();

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

        public Result ResolveApplicationControlPath(ref Fs.Path outPath, Ncm.ApplicationId applicationId, StorageId storageId)
        {
            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveApplicationControlPath(out Lr.Path path, applicationId);
            if (rc.IsFailure()) return rc;

            return SetUpFsPath(ref outPath, in path);
        }

        public Result ResolveApplicationHtmlDocumentPath(out bool isDirectory, ref Fs.Path outPath, Ncm.ApplicationId applicationId, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out isDirectory);

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveApplicationHtmlDocumentPath(out Lr.Path path, applicationId);
            if (rc.IsFailure()) return rc;

            isDirectory = PathUtility.IsDirectoryPath(path.Str);

            return SetUpFsPath(ref outPath, in path);
        }

        public Result ResolveProgramPath(out bool isDirectory, ref Fs.Path outPath, ProgramId programId, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out isDirectory);

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveProgramPath(out Lr.Path path, programId);
            if (rc.IsFailure()) return rc;

            isDirectory = PathUtility.IsDirectoryPath(path.Str);

            return SetUpFsPath(ref outPath, in path);
        }

        public Result ResolveRomPath(out bool isDirectory, ref Fs.Path outPath, ProgramId programId, StorageId storageId)
        {
            UnsafeHelpers.SkipParamInit(out isDirectory);

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveProgramPathForDebug(out Lr.Path path, programId);
            if (rc.IsFailure()) return rc;

            isDirectory = PathUtility.IsDirectoryPath(path.Str);

            return SetUpFsPath(ref outPath, in path);
        }

        public Result ResolveAddOnContentPath(ref Fs.Path outPath, DataId dataId)
        {
            Result rc = GetAddOnContentLocationResolver(out AddOnContentLocationResolver resolver);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveAddOnContentPath(out Lr.Path path, dataId);
            if (rc.IsFailure()) return rc;

            return SetUpFsPath(ref outPath, in path);
        }

        public Result ResolveDataPath(ref Fs.Path outPath, DataId dataId, StorageId storageId)
        {
            if (storageId == StorageId.None)
                return ResultFs.InvalidAlignment.Log();

            Result rc = GetLocationResolver(out LocationResolver resolver, storageId);
            if (rc.IsFailure()) return rc;

            rc = resolver.ResolveDataPath(out Lr.Path path, dataId);
            if (rc.IsFailure()) return rc;

            return SetUpFsPath(ref outPath, in path);
        }

        public Result ResolveRegisteredProgramPath(ref Fs.Path outPath, ulong id)
        {
            _fsServer.InitializeLocationResolverSet();

            RegisteredLocationResolver resolver = null;
            try
            {
                Result rc = GetRegisteredLocationResolver(out resolver);
                if (rc.IsFailure()) return rc;

                rc = resolver.ResolveProgramPath(out Lr.Path path, new ProgramId(id));
                if (rc.IsFailure()) return rc;

                return SetUpFsPath(ref outPath, in path);
            }
            finally
            {
                resolver?.Dispose();
            }
        }

        public Result ResolveRegisteredHtmlDocumentPath(ref Fs.Path outPath, ulong id)
        {
            _fsServer.InitializeLocationResolverSet();

            RegisteredLocationResolver resolver = null;
            try
            {
                Result rc = GetRegisteredLocationResolver(out resolver);
                if (rc.IsFailure()) return rc;

                rc = resolver.ResolveHtmlDocumentPath(out Lr.Path path, new ProgramId(id));
                if (rc.IsFailure()) return rc;

                return SetUpFsPath(ref outPath, in path);
            }
            finally
            {
                resolver?.Dispose();
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
    }
}
