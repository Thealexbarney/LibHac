// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem;

public interface IAesXtsFileHeader
{
    public interface IFileSystemContext
    {
    }

    public interface IFileContext
    {
    }
}

public struct AesXtsFileHeaderV0 : IAesXtsFileHeader
{
    public struct FileSystemContext : IAesXtsFileHeader.IFileSystemContext
    {
        public Array16<byte> MacKey;
        public Array16<byte> EncryptionKeyGenerationKey;
        public RandomDataGenerator GenerateRandomData;
    }

    public struct FileContext : IAesXtsFileHeader.IFileContext
    {
        public Array16<byte> MacKey;
        public Array32<byte> Keys;
    }

    public readonly bool HasValidSignature()
    {
        throw new NotImplementedException();
    }

    public static bool IsValidSignature(uint signature)
    {
        throw new NotImplementedException();
    }

    public void Create(long fileSize, ref FileSystemContext context)
    {
        throw new NotImplementedException();
    }

    private readonly void ComputeMac(Span<byte> outMac, ReadOnlySpan<byte> macKey)
    {
        throw new NotImplementedException();
    }

    private readonly void ComputeKeys(Span<byte> outKeys, ReadOnlySpan<byte> path,
        ReadOnlySpan<byte> encryptionKeyGenerationKey)
    {
        throw new NotImplementedException();
    }

    private void Encrypt(ReadOnlySpan<byte> keys)
    {
        throw new NotImplementedException();
    }

    private void Decrypt(ReadOnlySpan<byte> keys)
    {
        throw new NotImplementedException();
    }

    public Result SignAndEncryptKeys(ReadOnlySpan<byte> path, in FileSystemContext fsContext)
    {
        throw new NotImplementedException();
    }

    public Result DecryptKeysAndVerify(ref FileContext fileContext, ReadOnlySpan<byte> path, in FileSystemContext fsContext)
    {
        throw new NotImplementedException();
    }

    public Result Update(long fileSize, ref FileContext context)
    {
        throw new NotImplementedException();
    }

    public Result CreateStorage(ref UniqueRef<IStorage> outStorage, in FileSystemContext fsContext,
        IStorage baseStorage, ReadOnlySpan<byte> iv, int blockSize)
    {
        throw new NotImplementedException();
    }
}

public class AesXtsFile<THeader, TFileContext, TFsContext> : IFile where THeader : IAesXtsFileHeader
    where TFileContext : IAesXtsFileHeader.IFileContext
    where TFsContext : IAesXtsFileHeader.IFileSystemContext
{
    private UniqueRef<IFile> _baseFile;
    private UniqueRef<IStorage> _fileStorage;
    private UniqueRef<IStorage> _fileSubStorage;
    private UniqueRef<IStorage> _aesXtsStorage;
    private UniqueRef<IStorage> _alignmentMatchingStorage;
    private UniqueRef<IFile> _storageFile;
    private long _fileSize;
    private TFileContext _context;
    private OpenMode _openMode;

    public AesXtsFile()
    {
        throw new NotImplementedException();
    }

    public override void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(OpenMode mode, ref UniqueRef<IFile> baseFile, ReadOnlySpan<byte> path,
        in TFsContext fsContext, ReadOnlySpan<byte> iv, int blockSize)
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

public class AesXtsFileV0 : AesXtsFile<AesXtsFileHeaderV0, AesXtsFileHeaderV0.FileContext, AesXtsFileHeaderV0.FileSystemContext>;