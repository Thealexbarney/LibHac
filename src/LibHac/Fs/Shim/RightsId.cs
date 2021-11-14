using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Ncm;
using LibHac.Spl;

namespace LibHac.Fs.Shim;

public static class RightsIdShim
{
    public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, ProgramId programId,
        StorageId storageId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.GetRightsId(out rightsId, programId, storageId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out rightsId);

        Result rc = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        rc = fileSystemProxy.Get.GetRightsIdByPath(out rightsId, in sfPath);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result GetRightsId(this FileSystemClient fs, out RightsId rightsId, out byte keyGeneration, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out rightsId, out keyGeneration);

        Result rc = PathUtility.ConvertToFspPath(out FspPath sfPath, path);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        rc = fileSystemProxy.Get.GetRightsIdAndKeyGenerationByPath(out rightsId, out keyGeneration, in sfPath);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result RegisterExternalKey(this FileSystemClient fs, in RightsId rightsId, in AccessKey key)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.RegisterExternalKey(in rightsId, in key);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result UnregisterExternalKey(this FileSystemClient fs, ref RightsId rightsId)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.UnregisterExternalKey(in rightsId);
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public static Result UnregisterAllExternalKey(this FileSystemClient fs)
    {
        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result rc = fileSystemProxy.Get.UnregisterAllExternalKey();
        fs.Impl.AbortIfNeeded(rc);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }
}
