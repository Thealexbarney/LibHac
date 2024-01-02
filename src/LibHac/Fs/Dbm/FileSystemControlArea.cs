// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;

namespace LibHac.Fs.Dbm;

public class FileSystemControlArea : IDisposable
{
    private ValueSubStorage _storage;

    public FileSystemControlArea()
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public void Initialize(in ValueSubStorage storage)
    {
        throw new NotImplementedException();
    }

    public void FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public void ExpandAllocationTableInfo(uint newBlockCount)
    {
        throw new NotImplementedException();
    }

    private Result WriteStorageInfo(long offset, uint size, uint controlAreaOffset)
    {
        throw new NotImplementedException();
    }

    private Result ReadStorageInfo(out long outOffset, out uint outSize, uint controlAreaOffset)
    {
        throw new NotImplementedException();
    }

    private Result WriteAllocationInfo(uint index, uint controlAreaOffset)
    {
        throw new NotImplementedException();
    }

    private Result ReadAllocationInfo(out uint outIndex, uint controlAreaOffset)
    {
        throw new NotImplementedException();
    }

    public Result ReadAllocationTableInfo(out long outOffset, out uint outSize)
    {
        throw new NotImplementedException();
    }

    public Result ReadBlockSize(out long outBlockSize)
    {
        throw new NotImplementedException();
    }

    public Result ReadDataBodyInfo(out long outOffset, out uint outSize)
    {
        throw new NotImplementedException();
    }

    public Result WriteAllocationTableInfo(long offset, uint size)
    {
        throw new NotImplementedException();
    }

    public Result WriteDataBodyInfo(long offset, uint size)
    {
        throw new NotImplementedException();
    }

    public Result ReadDirectoryEntryInfo(out uint outIndex)
    {
        throw new NotImplementedException();
    }

    public Result ReadFileEntryInfo(out uint outIndex)
    {
        throw new NotImplementedException();
    }

    public Result WriteBlockSize(long size)
    {
        throw new NotImplementedException();
    }

    public Result WriteDirectoryEntryInfo(uint index)
    {
        throw new NotImplementedException();
    }

    public Result WriteFileEntryInfo(uint index)
    {
        throw new NotImplementedException();
    }
}