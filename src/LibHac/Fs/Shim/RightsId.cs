using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Spl;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for working with rights IDs and external keys for NCA encryption.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class RightsIdShim
{
    public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, ProgramId programId,
        StorageId storageId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.GetRightsId(out rightsId, programId, storageId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, U8Span path, ContentAttributes attributes)
    {
        return GetRightsId(fs, out rightsId, out _, path, attributes);
    }

    public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, out byte keyGeneration,
        U8Span path, ContentAttributes attributes)
    {
        UnsafeHelpers.SkipParamInit(out rightsId, out keyGeneration);

        Result res = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        res = fileSystemProxy.Get.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, in sfPath, attributes);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result RegisterExternalKey(this FileSystemClient fs, in RightsId rightsId, in AccessKey key)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.RegisterExternalKey(in rightsId, in key);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result UnregisterExternalKey(this FileSystemClient fs, ref RightsId rightsId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.UnregisterExternalKey(in rightsId);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    public static Result UnregisterAllExternalKey(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.UnregisterAllExternalKey();
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}