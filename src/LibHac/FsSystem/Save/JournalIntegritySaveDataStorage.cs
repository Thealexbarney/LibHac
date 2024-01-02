// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSystem.Save;

public class JournalIntegritySaveDataStorage : IStorage
{
    private JournalStorage _journalStorage;
    private HierarchicalIntegrityVerificationStorage _integrityStorage;

    public JournalIntegritySaveDataStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static Result QuerySize(
        out long outSizeJournalTable,
        out long outSizeJournalBitmapUpdatedPhysical,
        out long outSizeJournalBitmapUpdatedVirtual,
        out long outSizeJournalBitmapUnassigned,
        out HierarchicalIntegrityVerificationSizeSet outIntegritySizeSet,
        long sizeBlock,
        long dataSize,
        long journalSize)
    {
        throw new NotImplementedException();
    }

    public static Result Format(
        in ValueSubStorage storageJournalControlArea,
        in ValueSubStorage storageJournalTable,
        in ValueSubStorage storageJournalBitmapPhysical,
        in ValueSubStorage storageJournalBitmapVirtual,
        in ValueSubStorage storageJournalBitmapUnassigned,
        in ValueSubStorage storageIntegrityControlArea,
        in ValueSubStorage storageMasterHash,
        in HierarchicalIntegrityVerificationInformation metaIntegrity,
        long sizeBlock,
        long sizeData,
        long sizeReservedArea)
    {
        throw new NotImplementedException();
    }

    public static Result Expand(
        in ValueSubStorage storageJournalControlArea,
        in ValueSubStorage storageJournalTable,
        in ValueSubStorage storageJournalBitmapPhysical,
        in ValueSubStorage storageJournalBitmapVirtual,
        in ValueSubStorage storageJournalBitmapUnassigned,
        in ValueSubStorage storageIntegrityControlArea,
        in HierarchicalIntegrityVerificationInformation metaNew,
        long sizeUsableAreaNew,
        long sizeReservedAreaNew)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(
        in ValueSubStorage storageJournalControlArea,
        in ValueSubStorage storageJournalTable,
        in ValueSubStorage storageJournalBitmapPhysical,
        in ValueSubStorage storageJournalBitmapVirtual,
        in ValueSubStorage storageJournalBitmapUnassigned,
        in ValueSubStorage storageMasterHash,
        in ValueSubStorage storageIntegrityLayeredHashL1,
        in ValueSubStorage storageIntegrityLayeredHashL2,
        in ValueSubStorage storageIntegrityLayeredHashL3,
        in ValueSubStorage storageIntegritySaveData,
        in HierarchicalIntegrityVerificationInformation integrityInfo,
        FileSystemBufferManagerSet buffers,
        IHash256GeneratorFactory hashGeneratorFactory,
        bool isHashSaltEnabled,
        SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(
        in ValueSubStorage storageJournalControlArea,
        in ValueSubStorage storageJournalTable,
        in ValueSubStorage storageJournalBitmapPhysical,
        in ValueSubStorage storageJournalBitmapVirtual,
        in ValueSubStorage storageJournalBitmapUnassigned,
        in ValueSubStorage storageMasterHash,
        in ValueSubStorage storageIntegrityLayeredHashL1,
        in ValueSubStorage storageIntegrityLayeredHashL2,
        in ValueSubStorage storageIntegrityLayeredHashL3,
        in ValueSubStorage storageIntegritySaveData,
        in HierarchicalIntegrityVerificationInformation integrityInfo,
        FileSystemBufferManagerSet buffers,
        IHash256GeneratorFactory hashGeneratorFactory,
        bool isHashSaltEnabled,
        SdkRecursiveMutex mutex,
        Semaphore readSemaphore,
        Semaphore writeSemaphore)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public long GetBlockSize()
    {
        throw new NotImplementedException();
    }

    public long GetDataAreaSize()
    {
        throw new NotImplementedException();
    }

    public long GetReservedAreaSize()
    {
        throw new NotImplementedException();
    }

    public FileSystemBufferManagerSet GetBufferManagerSet()
    {
        throw new NotImplementedException();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public override Result Flush()
    {
        throw new NotImplementedException();
    }

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    public Result Commit()
    {
        throw new NotImplementedException();
    }

    public Result OnRollback()
    {
        throw new NotImplementedException();
    }

    public Result AcceptVisitor(IInternalStorageFileSystemVisitor visitor)
    {
        throw new NotImplementedException();
    }
}