using System;

namespace LibHac.Bcat.Detail.Service
{
    [Flags]
    internal enum AccessControl
    {
        None = 0,
        Bit0 = 1 << 0,
        Bit1 = 1 << 1,
        Bit2 = 1 << 2,
        Bit3 = 1 << 3,
        Bit4 = 1 << 4,
        All = ~0
    }
}
