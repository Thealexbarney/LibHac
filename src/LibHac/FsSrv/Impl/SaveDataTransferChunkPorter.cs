// ReSharper disable UnusedMember.Local
#pragma warning disable CS0169 // Field is never used
using System;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Fs;
using LibHac.Sf;
using ISaveDataChunkExporter = LibHac.FsSrv.Sf.ISaveDataChunkExporter;
using IStorage = LibHac.Fs.IStorage;

namespace LibHac.FsSrv.Impl;

public struct SaveDataChunkDiffInfo
{
    public Array64<bool> IsDifferent;
}

public class SaveDataChunkExporter : ISaveDataChunkExporter
{
    private SharedRef<AesGcmSource> _aesGcmSource;
    private SharedRef<AesGcmSource.IEncryptor> _encryptor;
    private SaveDataDivisionExporter _parentExporter;
    private ushort _chunkId;
    private long _totalReadSize;
    private bool _isPullable;

    public SaveDataChunkExporter(SaveDataDivisionExporter parentExporter, ushort chunkId,
        ref readonly SharedRef<AesGcmSource.IEncryptor> encryptor, bool isPullable)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> baseStorage, bool isCompressed, in AesIv iv)
    {
        throw new NotImplementedException();
    }

    public Result Pull(out ulong outBytesRead, OutBuffer buffer, ulong size)
    {
        throw new NotImplementedException();
    }

    public Result PullCore(out ulong outReadSize, Span<byte> buffer)
    {
        throw new NotImplementedException();
    }

    public Result GetRestRawDataSize(out long outRemainingSize)
    {
        throw new NotImplementedException();
    }

    public Result GetRestRawDataSizeCore(out long outRemainingSize)
    {
        throw new NotImplementedException();
    }
}

public class SaveDataChunkImporter : ISaveDataChunkImporter
{
    private AesMac _mac;
    private SharedRef<AesGcmSink> _sink;
    private SharedRef<AesGcmSink.IDecryptor> _decryptor;
    private SaveDataDivisionImporter _parentImporter;
    private ushort _chunkId;
    private bool _isImporterValid;

    public SaveDataChunkImporter(SaveDataDivisionImporter parentImporter, ushort chunkId,
        ref readonly SharedRef<AesGcmSink.IDecryptor> decryptor, in AesMac mac)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }

    public Result Initialize(ref readonly SharedRef<IStorage> storage, long size, bool isCompressionEnabled, bool useLargeBuffer)
    {
        throw new NotImplementedException();
    }

    public Result Push(ReadOnlySpan<byte> source)
    {
        throw new NotImplementedException();
    }
}