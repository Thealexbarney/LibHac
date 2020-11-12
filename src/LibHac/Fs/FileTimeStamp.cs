using System.Runtime.InteropServices;

namespace LibHac.Fs
{
    [StructLayout(LayoutKind.Sequential, Size = 0x20)]
    public struct FileTimeStampRaw
    {
        public long Created;
        public long Accessed;
        public long Modified;
        public bool IsLocalTime;
    }
}
