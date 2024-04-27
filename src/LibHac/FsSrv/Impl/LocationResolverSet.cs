using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Lr;
using LibHac.Ncm;
using LibHac.Os;
using LibHac.Util;

namespace LibHac.FsSrv.Impl;

public static class LocationResolverSetGlobalMethods
{
    public static void InitializeLocationResolverSet(this FileSystemServer fsServer)
    {
        ref LocationResolverSetGlobals globals = ref fsServer.Globals.LocationResolverSet;
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref globals.Mutex);

        if (!globals.IsLrInitialized)
        {
            fsServer.Hos.Lr.Initialize();
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
/// <remarks>Based on nnSdk 17.5.0 (FS 17.0.0)</remarks>
public class LocationResolverSet : IDisposable
{
    private Array5<Optional<LocationResolver>> _resolvers;
    private Optional<AddOnContentLocationResolver> _aocResolver;
    private SdkMutexType _mutex;

    // LibHac addition
    private FileSystemServer _fsServer;
    private HorizonClient Hos => _fsServer.Hos;

    public LocationResolverSet(FileSystemServer fsServer)
    {
        _mutex = new SdkMutexType();
        _fsServer = fsServer;
    }

    public void Dispose()
    {
        for (int i = 0; i < _resolvers.Length; i++)
        {
            if (_resolvers[i].HasValue)
            {
                _resolvers[i].Value.Dispose();
                _resolvers[i].Clear();
            }
        }

        if (_aocResolver.HasValue)
        {
            _aocResolver.Value.Dispose();
            _aocResolver.Clear();
        }
    }

    private static Result SetUpFsPath(ref Fs.Path outPath, ref readonly Lr.Path lrPath)
    {
        var flags = new PathFlags();
        flags.AllowMountName();

        if (Utility.IsHostFsMountName(lrPath.Value))
            flags.AllowWindowsPath();

        Result res = outPath.InitializeWithReplaceUnc(lrPath.Value);
        if (res.IsFailure()) return res.Miss();

        res = outPath.Normalize(flags);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    private Result GetLocationResolver(out LocationResolver resolver, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out resolver);

        _fsServer.InitializeLocationResolverSet();

        int index = GetResolverIndexFromStorageId(storageId);
        if (index == -1)
            return ResultLr.ApplicationNotFound.Log();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        // Open the location resolver if it hasn't been already
        if (!_resolvers[index].HasValue)
        {
            _resolvers[index].Set(null);
            Result res = Hos.Lr.OpenLocationResolver(out _resolvers[index].Value, storageId);

            if (res.IsFailure())
            {
                _resolvers[index].Clear();
                return ResultLr.ApplicationNotFound.Log();
            }
        }

        resolver = _resolvers[index].Value;
        return Result.Success;
    }

    private Result GetRegisteredLocationResolver(out RegisteredLocationResolver resolver)
    {
        _fsServer.InitializeLocationResolverSet();

        return Hos.Lr.OpenRegisteredLocationResolver(out resolver).Ret();
    }

    private Result GetAddOnContentLocationResolver(out AddOnContentLocationResolver resolver)
    {
        UnsafeHelpers.SkipParamInit(out resolver);

        _fsServer.InitializeLocationResolverSet();

        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (!_aocResolver.HasValue)
        {
            Result res = Hos.Lr.OpenAddOnContentLocationResolver(out AddOnContentLocationResolver lr);
            if (res.IsFailure()) return res.Miss();

            _aocResolver.Set(in lr);
        }

        resolver = _aocResolver.Value;
        return Result.Success;
    }

    public Result ResolveApplicationControlPath(ref Fs.Path outPath, out ContentAttributes outContentAttributes,
        Ncm.ApplicationId applicationId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outContentAttributes);

        Result res = GetLocationResolver(out LocationResolver resolver, storageId);
        if (res.IsFailure()) return res.Miss();

        res = resolver.ResolveApplicationControlPath(out Lr.Path path, applicationId);
        if (res.IsFailure()) return res.Miss();

        outContentAttributes = ContentAttributes.None;
        return SetUpFsPath(ref outPath, in path).Ret();
    }

    public Result ResolveApplicationHtmlDocumentPath(out bool outIsDirectory, ref Fs.Path outPath,
        out ContentAttributes outContentAttributes, ulong applicationId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outIsDirectory, out outContentAttributes);

        Result res = GetLocationResolver(out LocationResolver resolver, storageId);
        if (res.IsFailure()) return res.Miss();

        res = resolver.ResolveApplicationHtmlDocumentPath(out Lr.Path path, new ProgramId(applicationId));
        if (res.IsFailure()) return res.Miss();

        outContentAttributes = ContentAttributes.None;
        outIsDirectory = PathUtility.IsDirectoryPath(path.Value);

        return SetUpFsPath(ref outPath, in path).Ret();
    }

    public Result ResolveProgramPath(out bool outIsDirectory, ref Fs.Path outPath,
        out ContentAttributes outContentAttributes, ulong programId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outIsDirectory, out outContentAttributes);

        Result res = GetLocationResolver(out LocationResolver resolver, storageId);
        if (res.IsFailure()) return res.Miss();

        res = resolver.ResolveProgramPath(out Lr.Path path, new ProgramId(programId));
        if (res.IsFailure()) return res.Miss();

        outContentAttributes = ContentAttributes.None;
        outIsDirectory = PathUtility.IsDirectoryPath(path.Value);

        return SetUpFsPath(ref outPath, in path).Ret();
    }

    public Result ResolveRomPath(out bool outIsDirectory, ref Fs.Path outPath,
        out ContentAttributes outContentAttributes, ulong programId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outIsDirectory, out outContentAttributes);

        Result res = GetLocationResolver(out LocationResolver resolver, storageId);
        if (res.IsFailure()) return res.Miss();

        res = resolver.ResolveProgramPathForDebug(out Lr.Path path, new ProgramId(programId));
        if (res.IsFailure()) return res.Miss();

        outContentAttributes = ContentAttributes.None;
        outIsDirectory = PathUtility.IsDirectoryPath(path.Value);

        return SetUpFsPath(ref outPath, in path).Ret();
    }

    public Result ResolveAddOnContentPath(ref Fs.Path outPath, out ContentAttributes outContentAttributes,
        ref Fs.Path outPatchPath, out ContentAttributes outPatchContentAttributes, DataId dataId)
    {
        UnsafeHelpers.SkipParamInit(out outContentAttributes, out outPatchContentAttributes);

        Result res = GetAddOnContentLocationResolver(out AddOnContentLocationResolver resolver);
        if (res.IsFailure()) return res.Miss();

        res = resolver.GetRegisteredAddOnContentPaths(out Lr.Path path, out Lr.Path patchPath, dataId);
        if (res.IsFailure()) return res.Miss();

        outContentAttributes = ContentAttributes.None;

        if (patchPath.Value[0] != 0)
        {
            // Note: FS appears to assign the paths to the wrong outputs here
            res = SetUpFsPath(ref outPath, in patchPath);
            if (res.IsFailure()) return res.Miss();

            res = SetUpFsPath(ref outPatchPath, in path);
            if (res.IsFailure()) return res.Miss();

            outPatchContentAttributes = ContentAttributes.None;
        }
        else
        {
            res = SetUpFsPath(ref outPath, in path);
            if (res.IsFailure()) return res.Miss();
        }

        return Result.Success;
    }

    public Result ResolveDataPath(ref Fs.Path outPath, out ContentAttributes outContentAttributes, DataId dataId, StorageId storageId)
    {
        UnsafeHelpers.SkipParamInit(out outContentAttributes);

        if (storageId == StorageId.None)
            return ResultFs.InvalidAlignment.Log();

        Result res = GetLocationResolver(out LocationResolver resolver, storageId);
        if (res.IsFailure()) return res.Miss();

        res = resolver.ResolveDataPath(out Lr.Path path, dataId);
        if (res.IsFailure()) return res.Miss();

        outContentAttributes = ContentAttributes.None;
        return SetUpFsPath(ref outPath, in path).Ret();
    }

    public Result ResolveRegisteredProgramPath(ref Fs.Path outPath, out ContentAttributes outContentAttributes, ulong id)
    {
        UnsafeHelpers.SkipParamInit(out outContentAttributes);

        RegisteredLocationResolver resolver = null;
        try
        {
            Result res = GetRegisteredLocationResolver(out resolver);
            if (res.IsFailure()) return res.Miss();

            res = resolver.ResolveProgramPath(out Lr.Path path, new ProgramId(id));
            if (res.IsFailure()) return res.Miss();

            outContentAttributes = ContentAttributes.None;
            return SetUpFsPath(ref outPath, in path).Ret();
        }
        finally
        {
            resolver?.Dispose();
        }
    }

    public Result ResolveRegisteredHtmlDocumentPath(ref Fs.Path outPath, out ContentAttributes outContentAttributes, ulong id)
    {
        UnsafeHelpers.SkipParamInit(out outContentAttributes);

        RegisteredLocationResolver resolver = null;
        try
        {
            Result res = GetRegisteredLocationResolver(out resolver);
            if (res.IsFailure()) return res.Miss();

            res = resolver.ResolveHtmlDocumentPath(out Lr.Path path, new ProgramId(id));
            if (res.IsFailure()) return res.Miss();

            outContentAttributes = ContentAttributes.None;
            return SetUpFsPath(ref outPath, in path).Ret();
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
            StorageId.BuiltInSystem => 0,
            StorageId.BuiltInUser => 1,
            StorageId.Host => 2,
            StorageId.SdCard => 3,
            StorageId.GameCard => 4,
            _ => -1
        };
    }
}