namespace LibHac.Fat;

public struct FatReport
{
    public ushort FileCurrentOpenCount;
    public ushort FilePeakOpenCount;
    public ushort DirectoryCurrentOpenCount;
    public ushort DirectoryPeakOpenCount;
}

public struct FatReportInfo1
{
    public ushort FilePeakOpenCount;
    public ushort DirectoryPeakOpenCount;
}

public struct FatReportInfo2
{
    public ushort OpenUniqueFileEntryPeakCount;
    public ushort OpenUniqueDirectoryEntryPeakCount;
}