namespace LibHac.FsSrv
{
    public readonly struct InternalProgramIdRangeForStorageAccessSpeedControl
    {
        public readonly ulong ProgramIdWithoutPlatformIdMin;
        public readonly ulong ProgramIdWithoutPlatformIdMax;

        public InternalProgramIdRangeForStorageAccessSpeedControl(ulong min, ulong max)
        {
            ProgramIdWithoutPlatformIdMin = min;
            ProgramIdWithoutPlatformIdMax = max;
        }
    }
}

namespace LibHac.FsSrv.Impl
{
    internal struct StorageAccessSpeedControlGlobals
    {
        public InternalProgramIdRangeForStorageAccessSpeedControl TargetProgramIdRange;
    }

    /// <summary>
    /// Handles getting and setting the configuration for storage access speed control.
    /// </summary>
    /// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
    public static class StorageAccessSpeedControl
    {
        public static void SetTargetProgramIdRange(FileSystemServer fsServer,
            InternalProgramIdRangeForStorageAccessSpeedControl range)
        {
            fsServer.Globals.StorageAccessSpeedControl.TargetProgramIdRange = range;
        }

        public static bool IsTargetProgramId(FileSystemServer fsServer, ulong programId)
        {
            var range = fsServer.Globals.StorageAccessSpeedControl.TargetProgramIdRange;
            ulong programIdWithoutPlatformId = Utility.ClearPlatformIdInProgramId(programId);

            return programIdWithoutPlatformId >= range.ProgramIdWithoutPlatformIdMin &&
                   programIdWithoutPlatformId <= range.ProgramIdWithoutPlatformIdMax;
        }
    }
}