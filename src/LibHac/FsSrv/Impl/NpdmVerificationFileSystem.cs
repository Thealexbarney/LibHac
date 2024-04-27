using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Impl;

[InlineArray(0x20)]
public struct NpdmHash
{
    private byte _data;
}

public class NpdmVerificationFile : IFile
{
    private NpdmHash _hash;
    private UniqueRef<IFile> _baseFile;

    public NpdmVerificationFile(ref UniqueRef<IFile> baseFile, NpdmHash hash)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        _baseFile.Destroy();
        base.Dispose();
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoFlush()
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetSize(out long size)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

public class NpdmVerificationFileSystem : IFileSystem
{
    private NpdmHash _hash;
    private ReadOnlyFileSystem _baseFileSystem;

    public NpdmVerificationFileSystem(in SharedRef<IFileSystem> baseFileSystem, NpdmHash hash)
    {
        
    }

    public override void Dispose()
    {
        _baseFileSystem.Dispose();
        base.Dispose();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, ref readonly Path path, OpenMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateFile(ref readonly Path path, long size, CreateFileOptions option)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteFile(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCreateDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectory(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoDeleteDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCleanDirectoryRecursively(ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameFile(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRenameDirectory(ref readonly Path currentPath, ref readonly Path newPath)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, ref readonly Path path, OpenDirectoryMode mode)
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommit()
    {
        throw new NotImplementedException();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        throw new NotImplementedException();
    }

    protected override Result DoRollback()
    {
        throw new NotImplementedException();
    }

    protected override Result DoFlush()
    {
        throw new NotImplementedException();
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, ref readonly Path path)
    {
        throw new NotImplementedException();
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId, ref readonly Path path)
    {
        throw new NotImplementedException();
    }
}