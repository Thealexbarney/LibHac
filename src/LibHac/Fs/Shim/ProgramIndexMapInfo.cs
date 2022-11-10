using System;
using LibHac.Common;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.Fs.Shim;

/// <summary>
/// Contains functions for registering multi-program application
/// information of the currently running application with FS.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
public static class ProgramIndexMapInfoShim
{
    /// <summary>
    /// Unregisters any previously registered program index map info and registers the provided map info.
    /// </summary>
    /// <param name="fs">The <see cref="FileSystemClient"/> to use.</param>
    /// <param name="mapInfo">The program index map info entries to register.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PermissionDenied"/>: Insufficient permissions.</returns>
    public static Result RegisterProgramIndexMapInfo(this FileSystemClient fs,
        ReadOnlySpan<ProgramIndexMapInfo> mapInfo)
    {
        if (mapInfo.IsEmpty)
            return Result.Success;

        using SharedRef<IFileSystemProxy> fileSystemProxy = fs.Impl.GetFileSystemProxyServiceObject();

        Result res = fileSystemProxy.Get.RegisterProgramIndexMapInfo(InBuffer.FromSpan(mapInfo), mapInfo.Length);
        fs.Impl.AbortIfNeeded(res);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}