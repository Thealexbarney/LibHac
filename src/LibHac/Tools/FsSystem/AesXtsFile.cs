using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.Tools.FsSystem;

public class AesXtsFile : IFile
{
    private UniqueRef<IFile> _baseFile;
    private U8String Path { get; }
    private byte[] KekSeed { get; }
    private byte[] VerificationKey { get; }
    private int BlockSize { get; }
    private OpenMode Mode { get; }

    public AesXtsFileHeader Header { get; }
    private IStorage BaseStorage { get; }

    internal const int HeaderLength = 0x4000;

    public AesXtsFile(OpenMode mode, ref UniqueRef<IFile> baseFile, U8String path, ReadOnlySpan<byte> kekSeed, ReadOnlySpan<byte> verificationKey, int blockSize)
    {
        Mode = mode;
        Path = path;
        KekSeed = kekSeed.ToArray();
        VerificationKey = verificationKey.ToArray();
        BlockSize = blockSize;

        Header = new AesXtsFileHeader(baseFile.Get);

        baseFile.Get.GetSize(out long fileSize).ThrowIfFailure();

        if (!Header.TryDecryptHeader(Path.ToString(), KekSeed, VerificationKey))
        {
            ThrowHelper.ThrowResult(ResultFs.AesXtsFileSystemFileHeaderCorruptedOnFileOpen.Value, "NAX0 key derivation failed.");
        }

        if (HeaderLength + Alignment.AlignUp(Header.Size, 0x10) > fileSize)
        {
            ThrowHelper.ThrowResult(ResultFs.AesXtsFileSystemFileSizeCorruptedOnFileOpen.Value, "NAX0 key derivation failed.");
        }

        var fileStorage = new FileStorage(baseFile.Get);
        var encStorage = new SubStorage(fileStorage, HeaderLength, fileSize - HeaderLength);
        encStorage.SetResizable(true);

        BaseStorage = new CachedStorage(new Aes128XtsStorage(encStorage, Header.DecryptedKey1, Header.DecryptedKey2, BlockSize, true), 4, true);
        _baseFile = new UniqueRef<IFile>(ref baseFile);
    }

    public byte[] GetKey()
    {
        byte[] key = new byte[0x20];
        Array.Copy(Header.DecryptedKey1, 0, key, 0, 0x10);
        Array.Copy(Header.DecryptedKey2, 0, key, 0x10, 0x10);

        return key;
    }

    protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        UnsafeHelpers.SkipParamInit(out bytesRead);

        Result rc = DryRead(out long toRead, offset, destination.Length, in option, Mode);
        if (rc.IsFailure()) return rc;

        rc = BaseStorage.Read(offset, destination.Slice(0, (int)toRead));
        if (rc.IsFailure()) return rc;

        bytesRead = toRead;
        return Result.Success;
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        Result rc = DryWrite(out bool isResizeNeeded, offset, source.Length, in option, Mode);
        if (rc.IsFailure()) return rc;

        if (isResizeNeeded)
        {
            rc = DoSetSize(offset + source.Length);
            if (rc.IsFailure()) return rc;
        }

        rc = BaseStorage.Write(offset, source);
        if (rc.IsFailure()) return rc;

        if (option.HasFlushFlag())
        {
            return Flush();
        }

        return Result.Success;
    }

    protected override Result DoFlush()
    {
        return BaseStorage.Flush();
    }

    protected override Result DoGetSize(out long size)
    {
        size = Header.Size;
        return Result.Success;
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }

    protected override Result DoSetSize(long size)
    {
        Header.SetSize(size, VerificationKey);

        Result rc = _baseFile.Get.Write(0, Header.ToBytes(false));
        if (rc.IsFailure()) return rc;

        return BaseStorage.SetSize(Alignment.AlignUp(size, 0x10));
    }

    public override void Dispose()
    {
        BaseStorage.Flush();
        BaseStorage.Dispose();
        _baseFile.Destroy();

        base.Dispose();
    }
}