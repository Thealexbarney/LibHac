using System;
using System.Buffers;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.Os;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Manages creating and opening the filesystem access log file on the SD card, and writes messages to the log file.
/// </summary>
/// <remarks>Based on nnSdk 18.3.0 (FS 18.0.0)</remarks>
public class AccessLogSdCardWriter : IDisposable
{
    private bool _isLogFileOpen;
    private bool _isSetUp;
    private FileHandle _fileHandle;
    private byte[] _workBuffer;
    private long _logFilePosition;
    private int _bufferPosition;
    private SdkMutex _mutex;

    // Libhac addition
    private FileSystemClient _fsClient;

    private const int WorkBufferSize = 0x4000;

    private ReadOnlySpan<byte> AccessLogMountName => "$FsAccessLog"u8;
    private ReadOnlySpan<byte> AccessLogFilePath => "$FsAccessLog:/FsAccessLog.txt"u8;

    private ReadOnlySpan<byte> BomUtf8 => [0xEF, 0xBB, 0xBF];
    private ReadOnlySpan<byte> AccessLogStartMarker => "FS_ACCESS: { start_tag: true }\n"u8;
    private ReadOnlySpan<byte> AccessLogEndMarker => "FS_ACCESS: { end_tag: true }\n"u8;

    public AccessLogSdCardWriter(FileSystemClient fsClient)
    {
        _isLogFileOpen = false;
        _isSetUp = false;
        _workBuffer = null;
        _logFilePosition = 0;
        _bufferPosition = 0;
        _mutex = new SdkMutex();
        _fsClient = fsClient;
    }

    public void Dispose()
    {
        DeallocateWorkBuffer();
    }

    public void FinalizeObject()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);
        
        FlushBuffer();
        TearDown(writeEndTag: true);
        _isSetUp = true;
        
    }

    public void Flush()
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        FlushBuffer();
        TearDown(writeEndTag: true);
        _isSetUp = false;
    }

    public void AppendLog(ReadOnlySpan<byte> buffer, ulong processId)
    {
        using ScopedLock<SdkMutex> scopedLock = ScopedLock.Lock(ref _mutex);

        if (buffer.Length > 0)
        {
            AppendProcessId(processId);
            AppendBuffer(buffer);
        }
    }

    private bool SetUp()
    {
        if (_isSetUp)
            return _isLogFileOpen;

        _isSetUp = true;
        bool isSuccess = false;

        Result res = _fsClient.MountSdCard(AccessLogMountName);
        if (res.IsFailure()) return false;

        try
        {
            res = _fsClient.CreateFile(AccessLogFilePath, 0);
            if (res.IsSuccess() || ResultFs.PathAlreadyExists.Includes(res))
            {
                res = _fsClient.OpenFile(out _fileHandle, AccessLogFilePath, OpenMode.All);
                if (res.IsSuccess())
                {
                    try
                    {
                        if (WriteStartMarker())
                        {
                            _isLogFileOpen = true;
                            isSuccess = true;
                            return true;
                        }
                    }
                    finally
                    {
                        if (!isSuccess)
                            _fsClient.CloseFile(_fileHandle);
                    }
                }
            }
        }
        finally
        {
            if (!isSuccess)
                _fsClient.Unmount(AccessLogMountName);
        }

        return false;
    }

    private bool WriteStartMarker()
    {
        Result res = _fsClient.GetFileSize(out _logFilePosition, _fileHandle);
        if (res.IsFailure()) return false;

        if (_logFilePosition <= 0)
        {
            res = _fsClient.WriteFile(_fileHandle, 0, BomUtf8, WriteOption.Flush);
            if (res.IsFailure()) return false;

            _logFilePosition = BomUtf8.Length;
        }

        res = _fsClient.WriteFile(_fileHandle, _logFilePosition, AccessLogStartMarker, WriteOption.Flush);
        if (res.IsFailure()) return false;

        _logFilePosition += AccessLogStartMarker.Length;
        return true;
    }

    private void AppendBuffer(ReadOnlySpan<byte> buffer)
    {
        if (SetUp() && AllocateWorkBuffer())
        {
            if (buffer.Length > WorkBufferSize)
            {
                WriteWorkBuffer();
                Write(buffer);
            }
            else
            {
                if ((long)_bufferPosition + buffer.Length > WorkBufferSize)
                    WriteWorkBuffer();

                buffer.CopyTo(_workBuffer.AsSpan(_bufferPosition));
                _bufferPosition += buffer.Length;
            }
        }
    }

    private void AppendProcessId(ulong processId)
    {
        Span<byte> buffer = stackalloc byte[0x16];

        var sb = new U8StringBuilder(buffer);
        sb.Append("(0x"u8).AppendFormat(processId, 'X', 16).Append(") "u8);

        AppendBuffer(sb.Buffer);
    }

    private bool AllocateWorkBuffer()
    {
        if (_workBuffer is not null)
            return true;

        _workBuffer = ArrayPool<byte>.Shared.Rent(WorkBufferSize);
        return _workBuffer is not null;
    }

    private void DeallocateWorkBuffer()
    {
        if (_workBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(_workBuffer);
            _workBuffer = null;
        }
    }

    private void TearDown(bool writeEndTag)
    {
        if (_isLogFileOpen)
        {
            if (writeEndTag)
            {
                _fsClient.WriteFile(_fileHandle, _logFilePosition, AccessLogEndMarker, WriteOption.Flush).IgnoreResult();
            }

            _fsClient.CloseFile(_fileHandle);
            _fsClient.Unmount(AccessLogMountName);
            _isLogFileOpen = false;
        }
    }

    private void Write(ReadOnlySpan<byte> buffer)
    {
        if (_isLogFileOpen)
        {
            if (_logFilePosition + buffer.Length < _logFilePosition)
            {
                TearDown(writeEndTag: true);
            }
            else
            {
                Result res = _fsClient.WriteFile(_fileHandle, _logFilePosition, buffer, WriteOption.None);

                if (res.IsSuccess())
                {
                    _logFilePosition += buffer.Length;
                }
                else
                {
                    TearDown(writeEndTag: false);
                }
            }
        }
    }

    private void WriteWorkBuffer()
    {
        if (_workBuffer is not null && _bufferPosition > 0)
        {
            Write(_workBuffer.AsSpan(0, _bufferPosition));
            _bufferPosition = 0;
        }
    }

    private void FlushBuffer()
    {
        WriteWorkBuffer();

        if (_isLogFileOpen)
        {
            Result res = _fsClient.FlushFile(_fileHandle);
            if (res.IsFailure())
            {
                TearDown(writeEndTag: false);
            }
        }
    }
}