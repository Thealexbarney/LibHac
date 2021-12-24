using LibHac.Common.FixedArrays;

namespace LibHac.Fs;

public struct UnpreparedRangeInfo
{
    public Range Range;
    public long FileSize;
    public long PreparedRangeSize;
    public long TotalReadSize;
    public int CompletionRate;
    public Array20<byte> Reserved;
}

public struct LazyLoadArguments
{
    public int GuideIndex;
    public Array60<byte> Reserved;
}