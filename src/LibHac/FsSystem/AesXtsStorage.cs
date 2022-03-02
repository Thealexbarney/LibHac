// ReSharper disable NotAccessedField.Local
using System;
using System.Buffers.Binary;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Os;

namespace LibHac.FsSystem;

/// <summary>
/// Reads and writes to an <see cref="IStorage"/> that's encrypted with AES-XTS-128.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class AesXtsStorage : IStorage
{
    public static readonly int AesBlockSize = Aes.BlockSize;
    public static readonly int KeySize = Aes.KeySize128;
    public static readonly int IvSize = Aes.KeySize128;

    private IStorage _baseStorage;
    private Array16<byte> _key1;
    private Array16<byte> _key2;
    private Array16<byte> _iv;
    private int _blockSize;
    private SdkMutexType _mutex;

    // LibHac addition: This field goes unused if initialized with a plain IStorage.
    // The original class uses a template for both the shared and non-shared IStorage which avoids needing this field.
    private SharedRef<IStorage> _baseStorageShared;

    public static void MakeAesXtsIv(Span<byte> outIv, long offset, int blockSize)
    {
        Assert.Equal(outIv.Length, IvSize);
        Assert.SdkRequiresGreaterEqual(offset, 0);
        Assert.SdkRequiresAligned((ulong)blockSize, AesBlockSize);

        BinaryPrimitives.WriteInt64BigEndian(outIv.Slice(sizeof(long)), offset / blockSize);
    }

    public AesXtsStorage(IStorage baseStorage, ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2, ReadOnlySpan<byte> iv,
        int blockSize)
    {
        _baseStorage = baseStorage;
        _blockSize = blockSize;
        _mutex = new SdkMutexType();

        Assert.SdkRequiresEqual(KeySize, key1.Length);
        Assert.SdkRequiresEqual(KeySize, key2.Length);
        Assert.SdkRequiresEqual(IvSize, iv.Length);
        Assert.SdkRequiresAligned((ulong)blockSize, AesBlockSize);

        key1.CopyTo(_key1.Items);
        key2.CopyTo(_key2.Items);
        iv.CopyTo(_iv.Items);
    }

    public AesXtsStorage(ref SharedRef<IStorage> baseStorage, ReadOnlySpan<byte> key1, ReadOnlySpan<byte> key2,
        ReadOnlySpan<byte> iv, int blockSize)
    {
        _baseStorageShared = SharedRef<IStorage>.CreateMove(ref baseStorage);
        _baseStorage = _baseStorageShared.Get;
        _blockSize = blockSize;
        _mutex = new SdkMutexType();

        Assert.SdkRequiresEqual(KeySize, key1.Length);
        Assert.SdkRequiresEqual(KeySize, key2.Length);
        Assert.SdkRequiresEqual(IvSize, iv.Length);
        Assert.SdkRequiresAligned((ulong)blockSize, AesBlockSize);

        key1.CopyTo(_key1.Items);
        key2.CopyTo(_key2.Items);
        iv.CopyTo(_iv.Items);
    }

    public override void Dispose()
    {
        _baseStorageShared.Destroy();

        base.Dispose();
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
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

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        throw new NotImplementedException();
    }
}