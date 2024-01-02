// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSystem.Save;

public class IntegritySaveDataStorage : IStorage
{
    private HierarchicalIntegrityVerificationStorage _integrityStorage;
    private bool _isModified;

    public IntegritySaveDataStorage()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public static Result QuerySize(out HierarchicalIntegrityVerificationSizeSet outIntegritySizeSet, long blockSize,
        long metaSize)
    {
        throw new NotImplementedException();
    }

    public static Result Format(in ValueSubStorage storageIntegrityControlArea, in ValueSubStorage storageMasterHash,
        in HierarchicalIntegrityVerificationMetaInformation metaIntegrity)
    {
        throw new NotImplementedException();
    }

    public Result Initialize(
        in ValueSubStorage storageMasterHash,
        in ValueSubStorage storageIntegrityLayeredHashL1,
        in ValueSubStorage storageIntegrityLayeredHashL2,
        in ValueSubStorage storageIntegrityLayeredHashL3,
        in ValueSubStorage storageIntegritySaveData,
        in HierarchicalIntegrityVerificationInformation info,
        FileSystemBufferManagerSet buffers,
        IHash256GeneratorFactory hashGeneratorFactory,
        SdkRecursiveMutex mutex)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public bool IsModified()
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Flush()
    {
        throw new NotImplementedException();
    }

    public override Result GetSize(out long size)
    {
        throw new NotImplementedException();
    }

    public override Result SetSize(long size)
    {
        throw new NotImplementedException();
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    public Result Commit()
    {
        throw new NotImplementedException();
    }
}