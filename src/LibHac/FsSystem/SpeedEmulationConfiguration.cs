using LibHac.Fs;
using LibHac.FsSrv;

namespace LibHac.FsSystem;

internal struct SpeedEmulationConfigurationGlobals
{
    public SpeedEmulationMode SpeedEmulationMode;
}

/// <summary>
/// Handles getting and setting the configuration for storage speed emulation.
/// </summary>
/// <remarks>Based on nnSdk 14.3.0</remarks>
internal static class SpeedEmulationConfiguration
{
    public static void SetSpeedEmulationMode(this FileSystemServer fsServer, SpeedEmulationMode mode)
    {
        fsServer.Globals.SpeedEmulationConfiguration.SpeedEmulationMode = mode;
    }

    public static SpeedEmulationMode GetSpeedEmulationMode(this FileSystemServer fsServer)
    {
        return fsServer.Globals.SpeedEmulationConfiguration.SpeedEmulationMode;
    }
}