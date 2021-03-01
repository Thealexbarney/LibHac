using System;

namespace LibHac.Fs
{
    public readonly struct MountHostOption
    {
        public readonly MountHostOptionFlag Flags;

        public MountHostOption(int flags)
        {
            Flags = (MountHostOptionFlag)flags;
        }

        public MountHostOption(MountHostOptionFlag flags)
        {
            Flags = flags;
        }

        public static MountHostOption None => new MountHostOption(MountHostOptionFlag.None);

        public static MountHostOption PseudoCaseSensitive =>
            new MountHostOption(MountHostOptionFlag.PseudoCaseSensitive);
    }

    [Flags]
    public enum MountHostOptionFlag
    {
        None = 0,
        PseudoCaseSensitive = 1
    }
}
