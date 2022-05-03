using System;

namespace LibHac.Fs
{
    public enum SpeedEmulationMode
    {
        None = 0,
        Faster = 1,
        Slower = 2,
        Random = 3
    }
}

namespace LibHac.Fs.Shim
{
    public static class SpeedEmulationShim
    {
        public static Result SetSpeedEmulationMode(this FileSystemClient fs, SpeedEmulationMode mode)
        {
            throw new NotImplementedException();
        }

        public static Result GetSpeedEmulationMode(this FileSystemClient fs, out SpeedEmulationMode outMode)
        {
            throw new NotImplementedException();
        }
    }
}