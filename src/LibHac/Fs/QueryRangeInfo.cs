using System.Runtime.InteropServices;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x40)]
    public struct QueryRangeInfo
    {
        public uint AesCtrKeyType;
        public uint SpeedEmulationType;
    }
}
