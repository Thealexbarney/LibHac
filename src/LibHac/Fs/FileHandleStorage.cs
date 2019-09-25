using System;

namespace LibHac.Fs
{
    public class FileHandleStorage : StorageBase
    {
        private const long InvalidSize = -1;
        private readonly object _locker = new object();

        private FileSystemManager FsManager { get; }
        private FileHandle Handle { get; }
        private long FileSize { get; set; } = InvalidSize;
        private bool CloseHandle { get; }

        public FileHandleStorage(FileHandle handle) : this(handle, false) { }

        public FileHandleStorage(FileHandle handle, bool closeHandleOnDispose)
        {
            Handle = handle;
            CloseHandle = closeHandleOnDispose;
            FsManager = Handle.File.Parent.FsManager;
        }

        public override Result Read(long offset, Span<byte> destination)
        {
            lock (_locker)
            {
                if (destination.Length == 0) return Result.Success;

                Result rc = UpdateSize();
                if (rc.IsFailure()) return rc;

                if (destination.Length < 0 || offset < 0) return ResultFs.ValueOutOfRange.Log();
                if (!IsRangeValid(offset, destination.Length, FileSize)) return ResultFs.ValueOutOfRange.Log();

                return FsManager.ReadFile(Handle, offset, destination);
            }
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source)
        {
            lock (_locker)
            {
                if (source.Length == 0) return Result.Success;

                Result rc = UpdateSize();
                if (rc.IsFailure()) return rc;

                if (source.Length < 0 || offset < 0) return ResultFs.ValueOutOfRange.Log();
                if (!IsRangeValid(offset, source.Length, FileSize)) return ResultFs.ValueOutOfRange.Log();

                return FsManager.WriteFile(Handle, offset, source);
            }
        }

        public override Result Flush()
        {
            return FsManager.FlushFile(Handle);
        }

        public override Result SetSize(long size)
        {
            FileSize = InvalidSize;

            return FsManager.SetFileSize(Handle, size);
        }

        public override Result GetSize(out long size)
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

            Result rc = FsManager.GetFileSize(out long fileSize, Handle);
            if (rc.IsFailure()) return rc;

            FileSize = fileSize;
            return Result.Success;
        }

        public override void Dispose()
        {
            if (CloseHandle)
            {
                FsManager.CloseFile(Handle);
            }
        }
    }
}
