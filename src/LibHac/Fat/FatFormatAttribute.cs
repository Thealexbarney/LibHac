namespace LibHac.Fat;

public readonly struct FatFormatAttribute
{
    public readonly ulong MinimumSectorCount;
    public readonly ulong MaximumSectorCount;
    public readonly uint HiddenSectorCount;
    public readonly short NumHeads;
    public readonly short SectorsPerTrack;
    public readonly short SectorsPerCluster;
    public readonly int FatTableEntrySizeBits;
    public readonly FatType FatType;
    public readonly uint Reserved;

    public FatFormatAttribute(ulong minimumSectorCount, ulong maximumSectorCount, uint hiddenSectorCount,
        short numHeads, short sectorsPerTrack, short sectorsPerCluster, int fatTableEntrySizeBits, FatType fatType)
    {
        MinimumSectorCount = minimumSectorCount;
        MaximumSectorCount = maximumSectorCount;
        HiddenSectorCount = hiddenSectorCount;
        NumHeads = numHeads;
        SectorsPerTrack = sectorsPerTrack;
        SectorsPerCluster = sectorsPerCluster;
        FatTableEntrySizeBits = fatTableEntrySizeBits;
        FatType = fatType;
        Reserved = 0;
    }
}

public enum FatType
{
    Fat12 = 0,
    Fat16 = 1,
    Fat32 = 2,
    FatEx = 3
}