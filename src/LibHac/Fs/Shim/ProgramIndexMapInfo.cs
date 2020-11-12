using System;
using System.Runtime.InteropServices;
using LibHac.FsSrv;
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

            IFileSystemProxy fsProxy = fs.GetFileSystemProxyServiceObject();

            var mapInfoBuffer = new InBuffer(MemoryMarshal.Cast<ProgramIndexMapInfo, byte>(mapInfo));

            return fsProxy.RegisterProgramIndexMapInfo(mapInfoBuffer, mapInfo.Length);
        }
    }
}
