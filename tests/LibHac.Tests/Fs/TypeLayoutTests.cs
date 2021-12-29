using System.Runtime.CompilerServices;
using LibHac.Fs;
using Xunit;
using static LibHac.Tests.Common.Layout;

namespace LibHac.Tests.Fs;

public class TypeLayoutTests
{
    [Fact]
    public static void SaveDataAttribute_Layout()
    {
        var s = new SaveDataAttribute();

        Assert.Equal(0x40, Unsafe.SizeOf<SaveDataAttribute>());

        Assert.Equal(0x00, GetOffset(in s, in s.ProgramId));
        Assert.Equal(0x08, GetOffset(in s, in s.UserId));
        Assert.Equal(0x18, GetOffset(in s, in s.StaticSaveDataId));
        Assert.Equal(0x20, GetOffset(in s, in s.Type));
        Assert.Equal(0x21, GetOffset(in s, in s.Rank));
        Assert.Equal(0x22, GetOffset(in s, in s.Index));
        Assert.Equal(0x24, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void SaveDataCreationInfo_Layout()
    {
        var s = new SaveDataCreationInfo();

        Assert.Equal(0x40, Unsafe.SizeOf<SaveDataCreationInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.Size));
        Assert.Equal(0x08, GetOffset(in s, in s.JournalSize));
        Assert.Equal(0x10, GetOffset(in s, in s.BlockSize));
        Assert.Equal(0x18, GetOffset(in s, in s.OwnerId));
        Assert.Equal(0x20, GetOffset(in s, in s.Flags));
        Assert.Equal(0x24, GetOffset(in s, in s.SpaceId));
        Assert.Equal(0x25, GetOffset(in s, in s.IsPseudoSaveData));
        Assert.Equal(0x26, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void SaveDataFilter_Layout()
    {
        var s = new SaveDataFilter();

        Assert.Equal(0x48, Unsafe.SizeOf<SaveDataFilter>());

        Assert.Equal(0, GetOffset(in s, in s.FilterByProgramId));
        Assert.Equal(1, GetOffset(in s, in s.FilterBySaveDataType));
        Assert.Equal(2, GetOffset(in s, in s.FilterByUserId));
        Assert.Equal(3, GetOffset(in s, in s.FilterBySaveDataId));
        Assert.Equal(4, GetOffset(in s, in s.FilterByIndex));
        Assert.Equal(5, GetOffset(in s, in s.Rank));
        Assert.Equal(8, GetOffset(in s, in s.Attribute));
    }

    [Fact]
    public static void SaveDataMetaInfo_Layout()
    {
        var s = new SaveDataMetaInfo();

        Assert.Equal(0x10, Unsafe.SizeOf<SaveDataMetaInfo>());

        Assert.Equal(0, GetOffset(in s, in s.Size));
        Assert.Equal(4, GetOffset(in s, in s.Type));
        Assert.Equal(5, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void HashSalt_Layout()
    {
        var s = new HashSalt();

        Assert.Equal(0x20, Unsafe.SizeOf<HashSalt>());

        Assert.Equal(0, GetOffset(in s, in s.Hash[0]));
    }

    [Fact]
    public static void SaveDataInfo_Layout()
    {
        var s = new SaveDataInfo();

        Assert.Equal(0x60, Unsafe.SizeOf<SaveDataInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.SaveDataId));
        Assert.Equal(0x08, GetOffset(in s, in s.SpaceId));
        Assert.Equal(0x09, GetOffset(in s, in s.Type));
        Assert.Equal(0x10, GetOffset(in s, in s.UserId));
        Assert.Equal(0x20, GetOffset(in s, in s.StaticSaveDataId));
        Assert.Equal(0x28, GetOffset(in s, in s.ProgramId));
        Assert.Equal(0x30, GetOffset(in s, in s.Size));
        Assert.Equal(0x38, GetOffset(in s, in s.Index));
        Assert.Equal(0x3A, GetOffset(in s, in s.Rank));
        Assert.Equal(0x3B, GetOffset(in s, in s.State));
        Assert.Equal(0x3C, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void SaveDataExtraData_Layout()
    {
        var s = new SaveDataExtraData();

        Assert.Equal(0x200, Unsafe.SizeOf<SaveDataExtraData>());

        Assert.Equal(0x00, GetOffset(in s, in s.Attribute));
        Assert.Equal(0x40, GetOffset(in s, in s.OwnerId));
        Assert.Equal(0x48, GetOffset(in s, in s.TimeStamp));
        Assert.Equal(0x50, GetOffset(in s, in s.Flags));
        Assert.Equal(0x58, GetOffset(in s, in s.DataSize));
        Assert.Equal(0x60, GetOffset(in s, in s.JournalSize));
        Assert.Equal(0x68, GetOffset(in s, in s.CommitId));
        Assert.Equal(0x70, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void CommitOption_Layout()
    {
        var s = new CommitOption();

        Assert.Equal(4, Unsafe.SizeOf<CommitOption>());

        Assert.Equal(0, GetOffset(in s, in s.Flags));
    }

    [Fact]
    public static void MemoryReportInfo_Layout()
    {
        var s = new MemoryReportInfo();

        Assert.Equal(0x80, Unsafe.SizeOf<MemoryReportInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.PooledBufferFreeSizePeak));
        Assert.Equal(0x08, GetOffset(in s, in s.PooledBufferRetriedCount));
        Assert.Equal(0x10, GetOffset(in s, in s.PooledBufferReduceAllocationCount));
        Assert.Equal(0x18, GetOffset(in s, in s.BufferManagerFreeSizePeak));
        Assert.Equal(0x20, GetOffset(in s, in s.BufferManagerRetriedCount));
        Assert.Equal(0x28, GetOffset(in s, in s.ExpHeapFreeSizePeak));
        Assert.Equal(0x30, GetOffset(in s, in s.BufferPoolFreeSizePeak));
        Assert.Equal(0x38, GetOffset(in s, in s.PatrolReadAllocateBufferSuccessCount));
        Assert.Equal(0x40, GetOffset(in s, in s.PatrolReadAllocateBufferFailureCount));
        Assert.Equal(0x48, GetOffset(in s, in s.BufferManagerTotalAllocatableSizePeak));
        Assert.Equal(0x50, GetOffset(in s, in s.BufferPoolAllocateSizeMax));
        Assert.Equal(0x58, GetOffset(in s, in s.PooledBufferFailedIdealAllocationCountOnAsyncAccess));
        Assert.Equal(0x60, GetOffset(in s, in s.Reserved));
    }
}