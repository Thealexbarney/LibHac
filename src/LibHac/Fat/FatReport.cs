namespace LibHac.Fat;

public struct FatReport
{
    public ushort FileCurrentOpenCount;
    public ushort FilePeakOpenCount;
    public ushort DirectoryCurrentOpenCount;
    public ushort DirectoryPeakOpenCount;
}

public struct FatReportInfo
{
    public ushort FilePeakOpenCount;
    public ushort DirectoryPeakOpenCount;
}