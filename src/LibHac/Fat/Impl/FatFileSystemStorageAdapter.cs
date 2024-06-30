using LibHac.Common;
using LibHac.Fs;

namespace LibHac.Fat.Impl;

public static class FatFileSystemStorageAdapter
{
    private static readonly FatFormatAttribute[] FormatAttributes =
    [
        new FatFormatAttribute(minimumSectorCount: 0x1,        maximumSectorCount: 0x1000,      hiddenSectorCount: 0x10,    numHeads: 0x2,  sectorsPerTrack: 0x10, sectorsPerCluster: 0x10,  fatTableEntrySizeBits: 0xC,  FatType.Fat12),
        new FatFormatAttribute(minimumSectorCount: 0x1000,     maximumSectorCount: 0x4000,      hiddenSectorCount: 0x10,    numHeads: 0x2,  sectorsPerTrack: 0x20, sectorsPerCluster: 0x10,  fatTableEntrySizeBits: 0xC,  FatType.Fat12),
        new FatFormatAttribute(minimumSectorCount: 0x4000,     maximumSectorCount: 0x8000,      hiddenSectorCount: 0x20,    numHeads: 0x2,  sectorsPerTrack: 0x20, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0xC,  FatType.Fat12),
        new FatFormatAttribute(minimumSectorCount: 0x8000,     maximumSectorCount: 0x10000,     hiddenSectorCount: 0x20,    numHeads: 0x4,  sectorsPerTrack: 0x20, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0xC,  FatType.Fat12),
        new FatFormatAttribute(minimumSectorCount: 0x10000,    maximumSectorCount: 0x20000,     hiddenSectorCount: 0x20,    numHeads: 0x8,  sectorsPerTrack: 0x20, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0xC,  FatType.Fat12),
        new FatFormatAttribute(minimumSectorCount: 0x20000,    maximumSectorCount: 0x40000,     hiddenSectorCount: 0x40,    numHeads: 0x8,  sectorsPerTrack: 0x20, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0x10, FatType.Fat16),
        new FatFormatAttribute(minimumSectorCount: 0x40000,    maximumSectorCount: 0x80000,     hiddenSectorCount: 0x40,    numHeads: 0x10, sectorsPerTrack: 0x20, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0x10, FatType.Fat16),
        new FatFormatAttribute(minimumSectorCount: 0x80000,    maximumSectorCount: 0xFC000,     hiddenSectorCount: 0x80,    numHeads: 0x10, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0x10, FatType.Fat16),
        new FatFormatAttribute(minimumSectorCount: 0xFC000,    maximumSectorCount: 0x1F8000,    hiddenSectorCount: 0x80,    numHeads: 0x20, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0x10, FatType.Fat16),
        new FatFormatAttribute(minimumSectorCount: 0x1F8000,   maximumSectorCount: 0x200000,    hiddenSectorCount: 0x80,    numHeads: 0x40, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x20,  fatTableEntrySizeBits: 0x10, FatType.Fat16),
        new FatFormatAttribute(minimumSectorCount: 0x200000,   maximumSectorCount: 0x3F0000,    hiddenSectorCount: 0x80,    numHeads: 0x40, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x40,  fatTableEntrySizeBits: 0x10, FatType.Fat16),
        new FatFormatAttribute(minimumSectorCount: 0x3F0000,   maximumSectorCount: 0x400000,    hiddenSectorCount: 0x80,    numHeads: 0x80, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x40,  fatTableEntrySizeBits: 0x10, FatType.Fat16),
        new FatFormatAttribute(minimumSectorCount: 0x414400,   maximumSectorCount: 0x7E0000,    hiddenSectorCount: 0x2000,  numHeads: 0x80, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x40,  fatTableEntrySizeBits: 0x20, FatType.Fat32),
        new FatFormatAttribute(minimumSectorCount: 0x7E0000,   maximumSectorCount: 0x4000000,   hiddenSectorCount: 0x2000,  numHeads: 0xFF, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x40,  fatTableEntrySizeBits: 0x20, FatType.Fat32),
        new FatFormatAttribute(minimumSectorCount: 0x4040000,  maximumSectorCount: 0x10000000,  hiddenSectorCount: 0x8000,  numHeads: 0xFF, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x100, fatTableEntrySizeBits: 0x20, FatType.FatEx),
        new FatFormatAttribute(minimumSectorCount: 0x10000000, maximumSectorCount: 0x40000000,  hiddenSectorCount: 0x10000, numHeads: 0xFF, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x200, fatTableEntrySizeBits: 0x20, FatType.FatEx),
        new FatFormatAttribute(minimumSectorCount: 0x40000000, maximumSectorCount: 0x100000000, hiddenSectorCount: 0x20000, numHeads: 0xFF, sectorsPerTrack: 0x3F, sectorsPerCluster: 0x400, fatTableEntrySizeBits: 0x20, FatType.FatEx)
    ];

    public static Result FormatDryRun(uint userAreaSectorCount, uint protectedAreaSectorCount)
    {
        ulong totalSectorCount = (ulong)userAreaSectorCount + protectedAreaSectorCount;
        Result res = GetFormatAttributes(out ReadOnlyRef<FatFormatAttribute> attributes, totalSectorCount);
        if (res.IsFailure()) return res.Miss();

        // Call nn::fat::detail::GetPfFormatParams

        return Result.Success;
    }

    private static Result GetFormatAttributes(out ReadOnlyRef<FatFormatAttribute> outAttributes, ulong sectorCount)
    {
        for (int i = 0; i < FormatAttributes.Length; i++)
        {
            ref readonly FatFormatAttribute attribute = ref FormatAttributes[i];
            if (attribute.MinimumSectorCount <= sectorCount && attribute.MaximumSectorCount >= sectorCount)
            {
                outAttributes = new(in attribute);
                return Result.Success;
            }
        }

        outAttributes = default;
        return ResultFs.FatFsFormatUnsupportedSize.Log();
    }
}