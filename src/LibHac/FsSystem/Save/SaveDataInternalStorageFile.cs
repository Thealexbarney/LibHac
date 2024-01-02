// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem.Save;

file class Sha2SaltHashFile : IFile
{
    public Sha2SaltHashFile()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(IStorage storage, IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        in HashSalt hashSalt)
    {
        throw new NotImplementedException();
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

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}

file class SaveDataInternalStorageFileAccessor : IInternalStorageFileSystemVisitor
{
    public delegate Result MemoryFunction(ReadOnlySpan<byte> buffer);

    public delegate Result FileFunction(IFile file);

    public SaveDataInternalStorageFileAccessor(U8Span name, OpenMode mode, FileFunction fileFunction,
        MemoryFunction memoryFunction, IHash256GeneratorFactorySelector hashGeneratorFactorySelector,
        in HashSalt hashSalt)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, IStorage storage)
    {
        throw new NotImplementedException();
    }

    public Result Visit(U8Span name, ReadOnlySpan<byte> buffer)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataInternalStorageFile : IFile
{
    private IInternalStorageFileSystem _fileSystem;
    private OpenMode _openMode;
    private Array64<byte> _name;
    private IHash256GeneratorFactorySelector _hashGeneratorFactorySelector;
    private HashSalt _hashSalt;

    public SaveDataInternalStorageFile(IInternalStorageFileSystem fileSystem, U8Span name, OpenMode openMode,
        IHash256GeneratorFactorySelector hashGeneratorFactorySelector, HashSalt hashSalt)
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
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

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}