using System;
using System.Runtime.InteropServices;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x40)]
    public struct QueryRangeInfo
    {
        public int AesCtrKeyType;
        public int SpeedEmulationType;

        public void Clear()
        {
            this = default;
        }

        public void Merge(in QueryRangeInfo other)
        {
            AesCtrKeyType |= other.AesCtrKeyType;
            SpeedEmulationType |= other.SpeedEmulationType;
        }

        [Flags]
        public enum AesCtrKeyTypeFlag
        {
            InternalKeyForSoftwareAes = 1 << 0,
            InternalKeyForHardwareAes = 1 << 1,
            ExternalKeyForHardwareAes = 1 << 2
        }
    }
}
