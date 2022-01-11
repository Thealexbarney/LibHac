using LibHac.Common.FixedArrays;
using LibHac.Time;

namespace LibHac.Fs;

public struct FileTimeStamp
{
    public PosixTime Created;
    public PosixTime Accessed;
    public PosixTime Modified;
    public bool IsLocalTime;
    public Array7<byte> Reserved;
}

public struct FileTimeStampRaw
{
    public long Created;
    public long Accessed;
    public long Modified;
    public bool IsLocalTime;
    public Array7<byte> Reserved;
}