﻿using System;

namespace LibHac.Fs
{
    public class FileHandleStorage : StorageBase
    {
        private const long InvalidSize = -1;
        private readonly object _locker = new object();

        private FileSystemClient FsClient { get; }
        private FileHandle Handle { get; }
        private long FileSize { get; set; } = InvalidSize;
        private bool CloseHandle { get; }

        public FileHandleStorage(FileHandle handle) : this(handle, false) { }

        public FileHandleStorage(FileHandle handle, bool closeHandleOnDispose)
        {
            Handle = handle;
            CloseHandle = closeHandleOnDispose;
            FsClient = Handle.File.Parent.FsClient;
        }

        protected override Result ReadImpl(long offset, Span<byte> destination)
        {
            lock (_locker)
            {
                if (destination.Length == 0) return Result.Success;

                Result rc = UpdateSize();
                if (rc.IsFailure()) return rc;

                if (!IsRangeValid(offset, destination.Length, FileSize)) return ResultFs.OutOfRange.Log();

                return FsClient.ReadFile(Handle, offset, destination);
            }
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source)
        {
            lock (_locker)
            {
                if (source.Length == 0) return Result.Success;

                Result rc = UpdateSize();
                if (rc.IsFailure()) return rc;

                if (!IsRangeValid(offset, source.Length, FileSize)) return ResultFs.OutOfRange.Log();

                return FsClient.WriteFile(Handle, offset, source);
            }
        }

        protected override Result FlushImpl()
        {
            return FsClient.FlushFile(Handle);
        }

        protected override Result SetSizeImpl(long size)
        {
            FileSize = InvalidSize;

            return FsClient.SetFileSize(Handle, size);
        }

        protected override Result GetSizeImpl(out long size)
        {
            size = default;

            Result rc = UpdateSize();
            if (rc.IsFailure()) return rc;

            size = FileSize;
            return Result.Success;
        }

        private Result UpdateSize()
        {
            if (FileSize != InvalidSize) return Result.Success;

            Result rc = FsClient.GetFileSize(out long fileSize, Handle);
            if (rc.IsFailure()) return rc;

            FileSize = fileSize;
            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (CloseHandle)
            {
                FsClient.CloseFile(Handle);
            }
        }
    }
}
