using System;
using LibHac.Fs;

namespace LibHac.FsClient
{
    public partial class FileSystemClient
    {
        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination)
        {
            return FsManager.ReadFile(handle, offset, destination);
        }

        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination, ReadOption options)
        {
            return FsManager.ReadFile(handle, offset, destination, options);
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination)
        {
            return FsManager.ReadFile(out bytesRead, handle, offset, destination);
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination, ReadOption options)
        {
            return FsManager.ReadFile(out bytesRead, handle, offset, destination, options);
        }

        public Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return FsManager.WriteFile(handle, source, offset, options);
        }

        public Result FlushFile(FileHandle handle)
        {
            return FsManager.FlushFile(handle);
        }

        public Result GetFileSize(out long fileSize, FileHandle handle)
        {
            return FsManager.GetFileSize(out fileSize, handle);
        }

        public Result SetFileSize(FileHandle handle, long size)
        {
            return FsManager.SetFileSize(handle, size);
        }

        public OpenMode GetFileOpenMode(FileHandle handle)
        {
            return FsManager.GetFileOpenMode(handle);
        }

        public void CloseFile(FileHandle handle)
        {
            FsManager.CloseFile(handle);
        }
    }
}
