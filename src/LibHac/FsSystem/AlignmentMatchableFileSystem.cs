using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem;

public class AlignmentMatchingFile : ForwardingFile
{
    private readonly OpenMode _openMode;

    public AlignmentMatchingFile(ref UniqueRef<IFile> baseFile, OpenMode openMode) : base(ref baseFile)
    {
        _openMode = openMode;
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        Assert.SdkNotNull(BaseFile);

        Result res = DryWrite(out bool needsAppend, offset, source.Length, in option, _openMode);
        if (res.IsFailure()) return res.Miss();

        if (needsAppend)
        {
            res = SetSize(offset + source.Length);
            if (res.IsFailure()) return res.Miss();
        }

        res = GetSize(out long fileSize);
        if (res.IsFailure()) return res.Miss();

        const int writeOffsetAlignment = 0x4000; // All writes to the base file must start on a multiple of this value
        const int writeSizeAlignment = 0x200; // All writes to the base file must end on a multiple of this value, or end at the end of the file

        // The offset and size of the head and tail blocks will be aligned to this value. 
        // Must be >= writeSizeAlignment and <= writeOffsetAlignment
        const int blockAlignment = 0x4000;

        long alignedStartOffset = Alignment.AlignDown(offset, writeOffsetAlignment);
        long endOffset = offset + source.Length;
        long alignedEndOffset = Math.Min(Alignment.AlignUp(endOffset, writeSizeAlignment), fileSize);

        if (alignedStartOffset == offset && alignedEndOffset == endOffset)
        {
            return BaseFile.Get.Write(offset, source, in option).Ret();
        }

        if (!_openMode.HasFlag(OpenMode.Read))
            return ResultFs.UnexpectedInAlignmentMatchableFileSystemA.Log();

        long alignedBufferEndOffset = Math.Min(Alignment.AlignUp(endOffset, blockAlignment), fileSize);
        int pooledBufferSize = (int)(alignedBufferEndOffset - alignedStartOffset);

        Assert.SdkLessEqual(pooledBufferSize, PooledBuffer.GetAllocatableSizeMax());

        using var pooledBuffer = new PooledBuffer();
        pooledBuffer.Allocate(pooledBufferSize, pooledBufferSize);
        Assert.SdkNotNull(pooledBuffer.GetBuffer());

        // Read the head block into the buffer if the start offset isn't aligned
        if (offset != alignedStartOffset)
        {
            long headEndOffset = Math.Min(Alignment.AlignUp(offset, blockAlignment), fileSize);
            int headSize = (int)(headEndOffset - alignedStartOffset);
            Span<byte> headBuffer = pooledBuffer.GetBuffer().Slice(0, headSize);

            res = BaseFile.Get.Read(out long readSizeActual, alignedStartOffset, headBuffer, ReadOption.None);
            if (res.IsFailure()) return res.Miss();

            if (headSize != readSizeActual)
                return ResultFs.UnexpectedInAlignmentMatchableFileSystemA.Log();
        }

        // In the case where the write size is small enough that it's entirely contained in the head block,
        // we don't need to check if we need to read a tail block
        bool headBlockCoversEntireBuffer = offset != alignedStartOffset &&
                                           alignedBufferEndOffset == Math.Min(Alignment.AlignUp(offset, blockAlignment), fileSize);

        // Read the tail block into the buffer if the end offset isn't aligned
        if (!headBlockCoversEntireBuffer && endOffset != alignedEndOffset)
        {
            long tailStartOffset = Alignment.AlignDown(endOffset, blockAlignment);
            int tailSize = (int)(alignedBufferEndOffset - tailStartOffset);
            Span<byte> tailBuffer = pooledBuffer.GetBuffer().Slice((int)(tailStartOffset - alignedStartOffset), tailSize);

            res = BaseFile.Get.Read(out long readSizeActual, tailStartOffset, tailBuffer, ReadOption.None);
            if (res.IsFailure()) return res.Miss();

            if (tailSize != readSizeActual)
                return ResultFs.UnexpectedInAlignmentMatchableFileSystemA.Log();
        }

        source.CopyTo(pooledBuffer.GetBuffer().Slice((int)(offset - alignedStartOffset)));

        Span<byte> writeBuffer = pooledBuffer.GetBuffer().Slice(0, (int)(alignedEndOffset - alignedStartOffset));
        res = BaseFile.Get.Write(alignedStartOffset, writeBuffer, in option);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }
}

public class AlignmentMatchableFileSystem : ForwardingFileSystem
{
    public AlignmentMatchableFileSystem(ref SharedRef<IFileSystem> baseFileSystem) : base(ref baseFileSystem) { }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        using var baseFile = new UniqueRef<IFile>();
        Result res = BaseFileSystem.Get.OpenFile(ref baseFile.Ref, in path, mode);
        if (res.IsFailure()) return res.Miss();

        if (!mode.HasFlag(OpenMode.Read))
        {
            using var alignmentMatchingFile = new UniqueRef<IFile>(new AlignmentMatchingFile(ref baseFile.Ref, mode));
            outFile.Set(ref alignmentMatchingFile.Ref);
        }
        else
        {
            outFile.Set(ref baseFile.Ref);
        }

        return Result.Success;
    }
}