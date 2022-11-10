using System;
using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSystem;

/// <summary>
/// Represents a sparse <see cref="IStorage"/> where blocks of empty data containing all
/// zeros are not written to disk in order to save space.
/// </summary>
/// <remarks><para>The <see cref="SparseStorage"/>'s <see cref="BucketTree"/> contains <see cref="IndirectStorage.Entry"/>
/// values describing which portions of the storage are empty. This is accomplished by using a standard
/// <see cref="IndirectStorage"/> where the second <see cref="IStorage"/> contains only zeros.</para>
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para></remarks>
public class SparseStorage : IndirectStorage
{
    private class ZeroStorage : IStorage
    {
        public override Result Read(long offset, Span<byte> destination)
        {
            Assert.SdkRequiresGreaterEqual(offset, 0);

            if (destination.Length > 0)
                destination.Clear();

            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source)
        {
            return ResultFs.UnsupportedWriteForZeroStorage.Log();
        }

        public override Result Flush()
        {
            return Result.Success;
        }

        public override Result GetSize(out long size)
        {
            size = long.MaxValue;
            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            return ResultFs.UnsupportedSetSizeForZeroStorage.Log();
        }

        public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return Result.Success;
        }
    }

    private ZeroStorage _zeroStorage;

    public SparseStorage()
    {
        _zeroStorage = new ZeroStorage();
    }

    public override void Dispose()
    {
        _zeroStorage.Dispose();
        base.Dispose();
    }

    public void Initialize(long size)
    {
        GetEntryTable().Initialize(NodeSize, size);
        SetZeroStorage();
    }

    public void SetDataStorage(in ValueSubStorage storage)
    {
        Assert.SdkRequires(IsInitialized());

        SetStorage(0, in storage);
        SetZeroStorage();
    }

    private void SetZeroStorage()
    {
        SetStorage(1, _zeroStorage, 0, long.MaxValue);
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        // Validate pre-conditions
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequires(IsInitialized());

        // Succeed if there's nothing to read
        if (destination.Length == 0)
            return Result.Success;

        if (GetEntryTable().IsEmpty())
        {
            Result rc = GetEntryTable().GetOffsets(out BucketTree.Offsets offsets);
            if (rc.IsFailure()) return rc.Miss();

            if (!offsets.IsInclude(offset, destination.Length))
                return ResultFs.OutOfRange.Log();

            destination.Clear();
        }
        else
        {
            var closure = new OperatePerEntryClosure { OutBuffer = destination, Offset = offset };

            Result rc = OperatePerEntry(offset, destination.Length, enableContinuousReading: false, verifyEntryRanges: true, ref closure,
                static (ref ValueSubStorage storage, long physicalOffset, long virtualOffset, long size, ref OperatePerEntryClosure closure) =>
                {
                    int bufferPosition = (int)(virtualOffset - closure.Offset);
                    Result rc = storage.Read(physicalOffset, closure.OutBuffer.Slice(bufferPosition, (int)size));
                    if (rc.IsFailure()) return rc.Miss();

                    return Result.Success;
                });
            if (rc.IsFailure()) return rc.Miss();
        }

        return Result.Success;
    }
}