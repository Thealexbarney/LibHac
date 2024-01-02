// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Fs.Dbm.Impl;
using IndexType = uint;

namespace LibHac.Fs.Dbm;

public class FileSystemObjectTemplate<TDirName, TFileName>
    where TDirName : unmanaged
    where TFileName : unmanaged
{
    private HierarchicalFileTableTemplate<TDirName, TFileName, DirectoryInfo, FileInfo> _fileTable;
    private AllocationTable _allocationTable;
    private long _blockSize;
    private uint _blockSizeShift;

    public FileSystemObjectTemplate()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(AllocationTable allocationTable, long blockSize,
        BufferedAllocationTableStorage directoryEntries, BufferedAllocationTableStorage fileEntries)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result CreateDirectory(U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenDirectory(out IndexType outIndex, out DirectoryObjectTemplate<TDirName, TFileName> outDirectory,
        U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenDirectory(out DirectoryObjectTemplate<TDirName, TFileName> outDirectory, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result CreateFile(out IndexType outIndex, U8Span fullPath, in FileOptionalInfo optionalInfo)
    {
        throw new NotImplementedException();
    }

    public Result DeleteFile(U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenFile(out IndexType outIndex, ref FileObjectTemplate<TDirName, TFileName> file, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result OpenFile(ref FileObjectTemplate<TDirName, TFileName> file, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    public Result GetEntryType(out DirectoryEntryType outEntryType, U8Span fullPath)
    {
        throw new NotImplementedException();
    }

    private uint RoundUpBlockSize(long size)
    {
        throw new NotImplementedException();
    }

    private Result ResizeFile(ref FileInfo fileInfo,
        in HierarchicalFileTableTemplate<TDirName, TFileName, DirectoryInfo, FileInfo>.DirectoryKey key, long newSize)
    {
        throw new NotImplementedException();
    }

    private Result GetNextAllocationBlock(out IndexType outNextIndex, out uint outBlockCount, IndexType index)
    {
        throw new NotImplementedException();
    }
}