using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Os;
using static LibHac.Fs.Impl.AccessLogStrings;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs.Impl
{
    internal enum WriteState
    {
        None,
        NeedsFlush,
        Failed,
    }

    internal class FileAccessor : IDisposable
    {
        private IFile _file;
        private FileSystemAccessor _parentFileSystem;
        private WriteState _writeState;
        private Result _lastResult;
        private OpenMode _openMode;
        private FilePathHash _filePathHash;
        // ReSharper disable once NotAccessedField.Local
        private int _pathHashIndex;

        internal FileSystemClient FsClient { get; }

        public FileAccessor(FileSystemClient fsClient, ref IFile file, FileSystemAccessor parentFileSystem,
            OpenMode mode)
        {
            FsClient = fsClient;

            _file = Shared.Move(ref file);
            _parentFileSystem = parentFileSystem;
            _openMode = mode;
        }

        public void Dispose()
        {
            if (_lastResult.IsSuccess() && _writeState == WriteState.NeedsFlush)
            {
                Abort.DoAbort(ResultFs.NeedFlush.Log(), "File needs flush before closing.");
            }

            _parentFileSystem?.NotifyCloseFile(this);
            _file?.Dispose();

            _file = null;
        }

        public OpenMode GetOpenMode() => _openMode;
        public WriteState GetWriteState() => _writeState;
        public FileSystemAccessor GetParent() => _parentFileSystem;

        public void SetFilePathHash(FilePathHash filePathHash, int index)
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

        public Result ReadWithoutCacheAccessLog(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            return _file.Read(out bytesRead, offset, destination, in option);
        }

        private Result ReadWithCacheAccessLog(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option, bool usePathCache, bool useDataCache)
        {
            throw new NotImplementedException();
        }

        public Result Read(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            Result rc;
            Span<byte> logBuffer = stackalloc byte[0x50];
            var handle = new FileHandle(this);

            if (_lastResult.IsFailure())
            {
                if (FsClient.Impl.IsEnabledAccessLog() && FsClient.Impl.IsEnabledHandleAccessLog(handle))
                {
                    Tick start = FsClient.Hos.Os.GetSystemTick();
                    rc = _lastResult;
                    Tick end = FsClient.Hos.Os.GetSystemTick();

                    var sb = new U8StringBuilder(logBuffer, true);
                    sb.Append(LogOffset).AppendFormat(offset)
                        .Append(LogSize).AppendFormat(destination.Length)
                        .Append(LogReadSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in bytesRead, rc));

                    FsClient.Impl.OutputAccessLog(rc, start, end, handle, new U8Span(logBuffer),
                        nameof(UserFile.ReadFile));
                }

                return _lastResult;
            }

            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            bool usePathCache = _parentFileSystem is not null && _filePathHash.Data != 0;

            // Todo: Call IsGlobalFileDataCacheEnabled
#pragma warning disable 162
            bool useDataCache = false && _parentFileSystem is not null && _parentFileSystem.IsFileDataCacheAttachable();
#pragma warning restore 162
            if (usePathCache || useDataCache)
            {
                return ReadWithCacheAccessLog(out bytesRead, offset, destination, in option, usePathCache,
                    useDataCache);
            }
            else
            {
                if (FsClient.Impl.IsEnabledAccessLog() && FsClient.Impl.IsEnabledHandleAccessLog(handle))
                {
                    Tick start = FsClient.Hos.Os.GetSystemTick();
                    rc = ReadWithoutCacheAccessLog(out bytesRead, offset, destination, in option);
                    Tick end = FsClient.Hos.Os.GetSystemTick();

                    var sb = new U8StringBuilder(logBuffer, true);
                    sb.Append(LogOffset).AppendFormat(offset)
                        .Append(LogSize).AppendFormat(destination.Length)
                        .Append(LogReadSize).AppendFormat(AccessLogImpl.DereferenceOutValue(in bytesRead, rc));

                    FsClient.Impl.OutputAccessLog(rc, start, end, handle, new U8Span(logBuffer),
                        nameof(UserFile.ReadFile));
                }
                else
                {
                    rc = ReadWithoutCacheAccessLog(out bytesRead, offset, destination, in option);
                }

                return rc;
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse
        }

        public Result Write(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            if (_lastResult.IsFailure())
                return _lastResult;

            using ScopedSetter<WriteState> setter =
                ScopedSetter<WriteState>.MakeScopedSetter(ref _writeState, WriteState.Failed);

            if (_filePathHash.Data != 0)
            {
                throw new NotImplementedException();
            }
            else
            {
                Result rc = UpdateLastResult(_file.Write(offset, source, in option));
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

            Result rc = UpdateLastResult(_file.Flush());
            if (rc.IsFailure()) return rc;

            setter.Set(WriteState.None);
            return Result.Success;
        }

        public Result SetSize(long size)
        {
            if (_lastResult.IsFailure())
                return _lastResult;

            WriteState oldWriteState = _writeState;
            using ScopedSetter<WriteState> setter =
                ScopedSetter<WriteState>.MakeScopedSetter(ref _writeState, WriteState.Failed);

            Result rc = UpdateLastResult(_file.SetSize(size));
            if (rc.IsFailure()) return rc;

            if (_filePathHash.Data != 0)
            {
                throw new NotImplementedException();
            }

            setter.Set(oldWriteState);
            return Result.Success;
        }

        public Result GetSize(out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            if (_lastResult.IsFailure())
                return _lastResult;

            return _file.GetSize(out size);
        }

        public Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            return _file.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }
    }
}
