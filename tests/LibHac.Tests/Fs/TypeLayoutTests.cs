using System.Runtime.CompilerServices;
using LibHac.Fs;
using LibHac.Fs.Impl;
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

    [Fact]
    public static void ApplicationInfo_Layout()
    {
        var s = new ApplicationInfo();

        Assert.Equal(0x20, Unsafe.SizeOf<ApplicationInfo>());

        Assert.Equal(0x0, GetOffset(in s, in s.ApplicationId));
        Assert.Equal(0x8, GetOffset(in s, in s.Version));
        Assert.Equal(0xC, GetOffset(in s, in s.LaunchType));
        Assert.Equal(0xD, GetOffset(in s, in s.IsMultiProgram));
        Assert.Equal(0xE, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void CodeVerificationData_Layout()
    {
        var s = new CodeVerificationData();

        Assert.Equal(0x124, Unsafe.SizeOf<CodeVerificationData>());

        Assert.Equal(0x000, GetOffset(in s, in s.Signature));
        Assert.Equal(0x100, GetOffset(in s, in s.Hash));
        Assert.Equal(0x120, GetOffset(in s, in s.HasData));
        Assert.Equal(0x121, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void DirectoryEntry_Layout()
    {
        var s = new DirectoryEntry();

        Assert.Equal(0x310, Unsafe.SizeOf<DirectoryEntry>());

        Assert.Equal(0x000, GetOffset(in s, in s.Name));
        Assert.Equal(0x301, GetOffset(in s, in s.Attributes));
        Assert.Equal(0x302, GetOffset(in s, in s.Reserved302));
        Assert.Equal(0x304, GetOffset(in s, in s.Type));
        Assert.Equal(0x305, GetOffset(in s, in s.Reserved305));
        Assert.Equal(0x308, GetOffset(in s, in s.Size));
    }

    [Fact]
    public static void EncryptionSeed_Layout()
    {
        var s = new EncryptionSeed();

        Assert.Equal(0x10, Unsafe.SizeOf<EncryptionSeed>());

        Assert.Equal(0, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void FileSystemProxyErrorInfo_Layout()
    {
        var s = new FileSystemProxyErrorInfo();

        Assert.Equal(0x80, Unsafe.SizeOf<FileSystemProxyErrorInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.RemountForDataCorruptionCount));
        Assert.Equal(0x04, GetOffset(in s, in s.UnrecoverableDataCorruptionByRemountCount));
        Assert.Equal(0x08, GetOffset(in s, in s.FatFsError));
        Assert.Equal(0x28, GetOffset(in s, in s.RecoveredByInvalidateCacheCount));
        Assert.Equal(0x2C, GetOffset(in s, in s.SaveDataIndexCount));
        Assert.Equal(0x30, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void StorageErrorInfo_Layout()
    {
        var s = new StorageErrorInfo();

        Assert.Equal(0x10, Unsafe.SizeOf<StorageErrorInfo>());

        Assert.Equal(0x0, GetOffset(in s, in s.NumActivationFailures));
        Assert.Equal(0x4, GetOffset(in s, in s.NumActivationErrorCorrections));
        Assert.Equal(0x8, GetOffset(in s, in s.NumReadWriteFailures));
        Assert.Equal(0xC, GetOffset(in s, in s.NumReadWriteErrorCorrections));
    }

    [Fact]
    public static void FileTimeStamp_Layout()
    {
        var s = new FileTimeStamp();

        Assert.Equal(0x20, Unsafe.SizeOf<FileTimeStamp>());

        Assert.Equal(0x00, GetOffset(in s, in s.Created));
        Assert.Equal(0x08, GetOffset(in s, in s.Accessed));
        Assert.Equal(0x10, GetOffset(in s, in s.Modified));
        Assert.Equal(0x18, GetOffset(in s, in s.IsLocalTime));
        Assert.Equal(0x19, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void FileTimeStampRaw_Layout()
    {
        var s = new FileTimeStampRaw();

        Assert.Equal(0x20, Unsafe.SizeOf<FileTimeStampRaw>());

        Assert.Equal(0x00, GetOffset(in s, in s.Created));
        Assert.Equal(0x08, GetOffset(in s, in s.Accessed));
        Assert.Equal(0x10, GetOffset(in s, in s.Modified));
        Assert.Equal(0x18, GetOffset(in s, in s.IsLocalTime));
        Assert.Equal(0x19, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void ProgramIndexMapInfo_Layout()
    {
        var s = new ProgramIndexMapInfo();

        Assert.Equal(0x20, Unsafe.SizeOf<ProgramIndexMapInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.ProgramId));
        Assert.Equal(0x08, GetOffset(in s, in s.MainProgramId));
        Assert.Equal(0x10, GetOffset(in s, in s.ProgramIndex));
        Assert.Equal(0x11, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void QueryRangeInfo_Layout()
    {
        var s = new QueryRangeInfo();

        Assert.Equal(0x40, Unsafe.SizeOf<QueryRangeInfo>());

        Assert.Equal(0x00, GetOffset(in s, in s.AesCtrKeyType));
        Assert.Equal(0x04, GetOffset(in s, in s.SpeedEmulationType));
        Assert.Equal(0x08, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void RightsId_Layout()
    {
        var s = new RightsId();

        Assert.Equal(0x10, Unsafe.SizeOf<RightsId>());

        Assert.Equal(0x00, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void RsaEncryptedKey_Layout()
    {
        var s = new RsaEncryptedKey();

        Assert.Equal(0x100, Unsafe.SizeOf<RsaEncryptedKey>());

        Assert.Equal(0x00, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void AesKey_Layout()
    {
        var s = new AesKey();

        Assert.Equal(0x10, Unsafe.SizeOf<AesKey>());

        Assert.Equal(0x00, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void InitialDataAad_Layout()
    {
        var s = new InitialDataAad();

        Assert.Equal(0x20, Unsafe.SizeOf<InitialDataAad>());

        Assert.Equal(0x00, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void KeySeed_Layout()
    {
        var s = new KeySeed();

        Assert.Equal(0x10, Unsafe.SizeOf<KeySeed>());

        Assert.Equal(0x00, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void InitialDataMac_Layout()
    {
        var s = new InitialDataMac();

        Assert.Equal(0x10, Unsafe.SizeOf<InitialDataMac>());

        Assert.Equal(0x00, GetOffset(in s, in s.Value));
    }

    [Fact]
    public static void ImportReportInfo_Layout()
    {
        var s = new ImportReportInfo();

        Assert.Equal(0x20, Unsafe.SizeOf<ImportReportInfo>());

        Assert.Equal(0, GetOffset(in s, in s.DiffChunkCount));
        Assert.Equal(1, GetOffset(in s, in s.DoubleDivisionDiffChunkCount));
        Assert.Equal(2, GetOffset(in s, in s.HalfDivisionDiffChunkCount));
        Assert.Equal(3, GetOffset(in s, in s.CompressionRate));
        Assert.Equal(4, GetOffset(in s, in s.Reserved));
    }

    [Fact]
    public static void CacheStorageInfo_Layout()
    {
        var s = new CacheStorageInfo();

        Assert.Equal(0x20, Unsafe.SizeOf<CacheStorageInfo>());

        Assert.Equal(0, GetOffset(in s, in s.Index));
        Assert.Equal(4, GetOffset(in s, in s.Reserved));
    }
}