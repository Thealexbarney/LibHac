using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs.Impl;

internal enum WriteState
{
    None,
    NeedsFlush,
    Failed,
}

/// <summary>
/// Provides access to a mount <see cref="IFile"/> and handles caching it.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
internal class FileAccessor : IDisposable
{
    private const string NeedFlushMessage = "Error: nn::fs::CloseFile() failed because the file was not flushed.\n";

    private UniqueRef<IFile> _file;
    private FileSystemAccessor _parentFileSystem;
    private WriteState _writeState;
    private Result _lastResult;
    private OpenMode _openMode;
    private Box<FilePathHash> _filePathHash;
    private int _pathHashIndex;

    internal HorizonClient Hos { get; }

    public FileAccessor(HorizonClient hosClient, ref UniqueRef<IFile> file, FileSystemAccessor parentFileSystem,
        OpenMode mode)
    {
        Hos = hosClient;

        _file = new UniqueRef<IFile>(ref file);
        _parentFileSystem = parentFileSystem;
        _writeState = WriteState.None;
        _lastResult = Result.Success;
        _openMode = mode;
    }

    public void Dispose()
    {
        if (_lastResult.IsSuccess() && _writeState == WriteState.NeedsFlush)
        {
            Hos.Fs.Impl.LogErrorMessage(ResultFs.NeedFlush.Value, NeedFlushMessage);
            Abort.DoAbort(ResultFs.NeedFlush.Value);
        }

        _parentFileSystem?.NotifyCloseFile(this);

        _file.Destroy();
    }

    public OpenMode GetOpenMode() => _openMode;
    public WriteState GetWriteState() => _writeState;
    public FileSystemAccessor GetParent() => _parentFileSystem;

    public void SetFilePathHash(Box<FilePathHash> filePathHash, int index)
    {
        _filePathHash = filePathHash;
        _pathHashIndex = index;
    }

    private Result UpdateLastResult(Result result)
    {
        if (!ResultFs.UsableSpaceNotEnough.Includes(result))
            _lastResult = result;

        return result;
    }

    private Result ReadWithCacheAccessLog(out long bytesRead, long offset, Span<byte> destination,
        in ReadOption option, bool usePathCache, bool useDataCache)
    {
        throw new NotImplementedException();
    }

    public Result ReadWithoutCacheAccessLog(out long bytesRead, long offset, Span<byte> destination,
        in ReadOption option)
    {
        return _file.Get.Read(out bytesRead, offset, destination, in option);
    }

    public Result Read(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
    {
        UnsafeHelpers.SkipParamInit(out bytesRead);

        Result rc;
        Span<byte> logBuffer = stackalloc byte[0x50];
        var handle = new FileHandle(this);

        if (_lastResult.IsFailure())
        {
            if (Hos.Fs.Impl.IsEnabledAccessLog() && Hos.Fs.Impl.IsEnabledHandleAccessLog(handle))
            {
                Tick start = Hos.Os.GetSystemTick();
                rc = _lastResult;
                Tick end = Hos.Os.GetSystemTick();

                var sb = new U8StringBuilder(logBuffer, true);
                sb.Append(LogOffset).AppendFormat(offset)
                    .Append(LogSize).AppendFormat(destination.Length)
                    .Append(LogReadSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in bytesRead, rc));

                Hos.Fs.Impl.OutputAccessLog(rc, start, end, handle, new U8Span(logBuffer),
                    nameof(UserFile.ReadFile));
            }

            return _lastResult;
        }

        bool usePathCache = _parentFileSystem is not null && _filePathHash is not null;
        bool useDataCache = Hos.Fs.Impl.IsGlobalFileDataCacheEnabled() && _parentFileSystem is not null &&
                            _parentFileSystem.IsFileDataCacheAttachable();

        if (usePathCache || useDataCache)
        {
            return ReadWithCacheAccessLog(out bytesRead, offset, destination, in option, usePathCache,
                useDataCache);
        }

        if (Hos.Fs.Impl.IsEnabledAccessLog() && Hos.Fs.Impl.IsEnabledHandleAccessLog(handle))
        {
            Tick start = Hos.Os.GetSystemTick();
            rc = ReadWithoutCacheAccessLog(out bytesRead, offset, destination, in option);
            Tick end = Hos.Os.GetSystemTick();

            var sb = new U8StringBuilder(logBuffer, true);
            sb.Append(LogOffset).AppendFormat(offset)
                .Append(LogSize).AppendFormat(destination.Length)
                .Append(LogReadSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in bytesRead, rc));

            Hos.Fs.Impl.OutputAccessLog(rc, start, end, handle, new U8Span(logBuffer),
                nameof(UserFile.ReadFile));
        }
        else
        {
            rc = ReadWithoutCacheAccessLog(out bytesRead, offset, destination, in option);
        }

        return rc;
    }

    public Result Write(long offset, ReadOnlySpan<byte> source, in WriteOption option)
    {
        if (_lastResult.IsFailure())
            return _lastResult;

        using ScopedSetter<WriteState> setter =
            ScopedSetter<WriteState>.MakeScopedSetter(ref _writeState, WriteState.Failed);

        if (_filePathHash is not null)
        {
            Result rc = UpdateLastResult(Hos.Fs.Impl.WriteViaPathBasedFileDataCache(_file.Get, (int)GetOpenMode(),
                _parentFileSystem, in _filePathHash.Value, _pathHashIndex, offset, source, in option));
            if (rc.IsFailure()) return rc.Miss();
        }
        else
        {
            Result rc = UpdateLastResult(_file.Get.Write(offset, source, in option));
            if (rc.IsFailure()) return rc;
        }

        setter.Set(option.HasFlushFlag() ? WriteState.None : WriteState.NeedsFlush);
        return Result.Success;
    }

    public Result Flush()
    {
        if (_lastResult.IsFailure())
            return _lastResult;

        using ScopedSetter<WriteState> setter =
            ScopedSetter<WriteState>.MakeScopedSetter(ref _writeState, WriteState.Failed);

        Result rc = UpdateLastResult(_file.Get.Flush());
        if (rc.IsFailure()) return rc;

        setter.Set(WriteState.None);
        return Result.Success;
    }

    public Result SetSize(long size)
    {
        if (_lastResult.IsFailure())
            return _lastResult;

        WriteState originalWriteState = _writeState;
        using ScopedSetter<WriteState> setter =
            ScopedSetter<WriteState>.MakeScopedSetter(ref _writeState, WriteState.Failed);

        if (_filePathHash is not null)
        {
            using UniqueLock lk = Hos.Fs.Impl.LockPathBasedFileDataCacheEntries();

            Result rc = UpdateLastResult(_file.Get.SetSize(size));
            if (rc.IsFailure()) return rc.Miss();

            Hos.Fs.Impl.InvalidatePathBasedFileDataCacheEntry(_parentFileSystem, in _filePathHash.Value, _pathHashIndex);
            if (rc.IsFailure()) return rc.Miss();
        }
        else
        {
            Result rc = UpdateLastResult(_file.Get.SetSize(size));
            if (rc.IsFailure()) return rc;
        }

        setter.Set(originalWriteState);
        return Result.Success;
    }

    public Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        if (_lastResult.IsFailure())
            return _lastResult;

        return _file.Get.GetSize(out size);
    }

    public Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
        ReadOnlySpan<byte> inBuffer)
    {
        return _file.Get.OperateRange(outBuffer, operationId, offset, size, inBuffer);
    }
}