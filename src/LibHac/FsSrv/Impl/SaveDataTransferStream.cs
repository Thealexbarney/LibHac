// ReSharper disable UnusedMember.Local UnusedType.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl;

public interface ISource : IDisposable
{
    Result Pull(out ulong outBytesRead, Span<byte> destination);
    Result GetRestRawDataSize(out long outSize);
    bool IsEnd();
}

public interface ISink : IDisposable
{
    Result Push(ReadOnlySpan<byte> data);
    Result FinalizeObject();
}

public interface IStream : ISource, ISink;

public class StorageStream : IStream
{
    private SharedRef<IStorage> _baseStorage;
    private long _size;
    private long _position;

    public StorageStream(ref readonly SharedRef<IStorage> storage)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Pull(out ulong outBytesRead, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public Result GetRestRawDataSize(out long outSize)
    {
        throw new NotImplementedException();
    }

    public bool IsEnd()
    {
        throw new NotImplementedException();
    }

    public Result Push(ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeObject()
    {
        throw new NotImplementedException();
    }
}

public class CompressionSource : ISource
{
    public CompressionSource(ref readonly SharedRef<ISource> source)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private bool IsInitialized()
    {
        throw new NotImplementedException();
    }

    public Result Initialize()
    {
        throw new NotImplementedException();
    }

    public Result Pull(out ulong outBytesRead, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public Result GetRestRawDataSize(out long outSize)
    {
        throw new NotImplementedException();
    }

    public bool IsEnd()
    {
        throw new NotImplementedException();
    }
}

public class DecompressionSink : ISink
{
    public DecompressionSink(ref readonly SharedRef<ISink> sink)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    private bool IsInitialized()
    {
        throw new NotImplementedException();
    }

    public Result Initialize()
    {
        throw new NotImplementedException();
    }

    public Result FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result Push(ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }

    private Result DecompressAndPushImpl(ReadOnlySpan<byte> source, bool isEndData)
    {
        throw new NotImplementedException();
    }
}

public struct AesGcmStreamHeader
{
    public uint Signature;
    public short Version;
    public short KeyGeneration;
    public Array8<byte> Field8;
    public Array16<byte> Iv;
}

public struct AesGcmStreamTail
{
    public Array16<byte> Mac;
}

public class AesGcmSource : ISource
{
    public interface IEncryptor : IDisposable
    {
        Result Initialize(ref AesGcmStreamHeader header, in AesIv iv);
        Result Update(Span<byte> destination, ReadOnlySpan<byte> source);
        Result GetMac(out AesMac outMac);
    }

    private long _restHeaderSize;
    private AesGcmStreamHeader _header;
    private SharedRef<ISource> _baseSource;
    private IEncryptor _encryptor;
    private AesIv _aesIv;
    private bool _isDataFinished;
    private long _restMacSize;
    private AesMac _aesMac;

    public AesGcmSource(ref readonly SharedRef<ISource> source, ref readonly SharedRef<IEncryptor> encryptor, in AesIv iv)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Initialize()
    {
        throw new NotImplementedException();
    }

    public Result Pull(out ulong outBytesRead, Span<byte> destination)
    {
        throw new NotImplementedException();
    }

    public Result GetRestRawDataSize(out long outSize)
    {
        throw new NotImplementedException();
    }

    public bool IsEnd()
    {
        throw new NotImplementedException();
    }

    public Result GetMac(out AesMac outMac)
    {
        throw new NotImplementedException();
    }
}

public class AesGcmSink : ISink
{
    public interface IDecryptor : IDisposable
    {
        Result Initialize(in AesGcmStreamHeader header);
        Result Update(Span<byte> destination, ReadOnlySpan<byte> source);
        Result Verify();
    }

    private long _restHeaderSize;
    private AesGcmStreamHeader _header;
    private SharedRef<ISink> _baseSink;
    private SharedRef<IDecryptor> _decryptor;
    private long _restSize;
    private AesMac _mac;
    private PooledBuffer _pooledBuffer;

    public AesGcmSink(ref readonly SharedRef<ISink> sink, long size, ref readonly SharedRef<IDecryptor> decryptor,
        bool useLargeBuffer)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Push(ReadOnlySpan<byte> data)
    {
        throw new NotImplementedException();
    }

    public Result FinalizeObject()
    {
        throw new NotImplementedException();
    }

    public Result GetMac(out AesMac outMac)
    {
        throw new NotImplementedException();
    }

    public bool IsEnd()
    {
        throw new NotImplementedException();
    }
}