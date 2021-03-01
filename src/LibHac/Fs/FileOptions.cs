using System;

namespace LibHac.Fs
{
    public readonly struct ReadOption
    {
        public readonly int Value;

        public ReadOption(int value)
        {
            Value = value;
        }

        public static ReadOption None => default;
    }

    public readonly struct WriteOption
    {
        public readonly WriteOptionFlag Flags;

        public WriteOption(int flags)
        {
            Flags = (WriteOptionFlag)flags;
        }

        public WriteOption(WriteOptionFlag flags)
        {
            Flags = flags;
        }

        public bool HasFlushFlag() => Flags.HasFlag(WriteOptionFlag.Flush);

        public static WriteOption None => new WriteOption(WriteOptionFlag.None);
        public static WriteOption Flush => new WriteOption(WriteOptionFlag.Flush);
    }

    [Flags]
    public enum WriteOptionFlag
    {
        None = 0,
        Flush = 1
    }
}
