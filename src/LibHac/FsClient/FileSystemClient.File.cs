using System;
using LibHac.Fs;

namespace LibHac.FsClient
{
    public partial class FileSystemClient
    {
        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination)
        {
            throw new NotImplementedException();
        }

        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination, ReadOption options)
        {
            throw new NotImplementedException();
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination)
        {
            throw new NotImplementedException();
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination, ReadOption options)
        {
            throw new NotImplementedException();
        }

        public Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source,  WriteOption options)
        {
            throw new NotImplementedException();
        }

        public Result FlushFile(FileHandle handle)
        {
            throw new NotImplementedException();
        }

        public Result GetFileSize(out long size, FileHandle handle)
        {
            throw new NotImplementedException();
        }

        public Result SetFileSize(FileHandle handle, long size)
        {
            throw new NotImplementedException();
        }

        public OpenMode GetFileOpenMode(FileHandle handle)
        {
            throw new NotImplementedException();
        }

        public void CloseFile(FileHandle handle)
        {
            throw new NotImplementedException();
        }
    }
}
