using System;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;

namespace LibHac.Tools.FsSystem;

public class AesCbcStorage : SectorStorage
{
    private const int BlockSize = 0x10;

    private readonly byte[] _key;
    private readonly byte[] _iv;

    private readonly long _size;

    public AesCbcStorage(IStorage baseStorage, ReadOnlySpan<byte> key, ReadOnlySpan<byte> iv,
        bool leaveOpen) : base(baseStorage, BlockSize, leaveOpen)
    {
        if (key.Length != BlockSize) throw new ArgumentException(nameof(key), $"Key must be {BlockSize} bytes long");
        if (iv.Length != BlockSize) throw new ArgumentException(nameof(iv), $"Counter must be {BlockSize} bytes long");

        _key = key.ToArray();
        _iv = iv.ToArray();

        baseStorage.GetSize(out _size).ThrowIfFailure();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        Result rc = CheckAccessRange(offset, destination.Length, _size);
        if (rc.IsFailure()) return rc.Miss();

        rc = base.Read(offset, destination);
        if (rc.IsFailure()) return rc;

        rc = GetDecryptor(out ICipher cipher, offset);
        if (rc.IsFailure()) return rc;

        cipher.Transform(destination, destination);

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        return ResultFs.UnsupportedOperation.Log();
    }

    public override Result Flush()
    {
        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        return ResultFs.UnsupportedOperation.Log();
    }

    private Result GetDecryptor(out ICipher decryptor, long offset)
    {
        if (offset == 0)
        {
            // Use the IV directly
            decryptor = Aes.CreateCbcDecryptor(_key, _iv);
            return Result.Success;
        }

        UnsafeHelpers.SkipParamInit(out decryptor);

        // Need to get the output of the previous block
        Span<byte> prevBlock = stackalloc byte[BlockSize];
        Result rc = BaseStorage.Read(offset - BlockSize, prevBlock);
        if (rc.IsFailure()) return rc;

        ICipher tmpDecryptor = Aes.CreateCbcDecryptor(_key, _iv);

        tmpDecryptor.Transform(prevBlock, prevBlock);

        decryptor = tmpDecryptor;
        return Result.Success;
    }
}