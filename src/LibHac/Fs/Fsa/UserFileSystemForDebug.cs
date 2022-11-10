using LibHac.Common;
using LibHac.Fs.Impl;

namespace LibHac.Fs.Fsa;

/// <summary>
/// Contains functions meant for debug use for interacting with mounted file systems.
/// </summary>
/// <remarks>Based on nnSdk 13.4.0 (FS 13.1.0)</remarks>
public static class UserFileSystemForDebug
{
    internal static Result GetFileTimeStampRawForDebug(this FileSystemClientImpl fs, out FileTimeStampRaw timeStamp,
        U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out timeStamp);

        Result res = fs.FindFileSystem(out FileSystemAccessor fileSystem, out U8Span subPath, path);
        if (res.IsFailure()) return res.Miss();

        return fileSystem.GetFileTimeStampRaw(out timeStamp, subPath);
    }

    public static Result GetFileTimeStampRawForDebug(this FileSystemClient fs, out FileTimeStampRaw timeStamp,
        U8Span path)
    {
        Result res = fs.Impl.GetFileTimeStampRawForDebug(out timeStamp, path);
        fs.Impl.AbortIfNeeded(res);
        return res;
    }
}