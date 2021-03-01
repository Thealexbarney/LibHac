using System;
using System.Runtime.InteropServices;
using LibHac.FsSrv.Sf;
using LibHac.Sf;

namespace LibHac.Fs.Shim
{
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

            using ReferenceCountedDisposable<IFileSystemProxy> fsProxy = fs.Impl.GetFileSystemProxyServiceObject();

            var mapInfoBuffer = new InBuffer(MemoryMarshal.Cast<ProgramIndexMapInfo, byte>(mapInfo));

            Result rc = fsProxy.Target.RegisterProgramIndexMapInfo(mapInfoBuffer, mapInfo.Length);
            fs.Impl.AbortIfNeeded(rc);
            return rc;
        }
    }
}
