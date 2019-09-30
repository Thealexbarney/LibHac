using System;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination)
        {
            return ReadFile(handle, offset, destination, ReadOption.None);
        }

        public Result ReadFile(FileHandle handle, long offset, Span<byte> destination, ReadOption option)
        {
            Result rc = ReadFile(out long bytesRead, handle, offset, destination, option);
            if (rc.IsFailure()) return rc;

            if (bytesRead == destination.Length) return Result.Success;

            return ResultFs.ValueOutOfRange.Log();
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination)
        {
            return ReadFile(out bytesRead, handle, offset, destination, ReadOption.None);
        }

        public Result ReadFile(out long bytesRead, FileHandle handle, long offset, Span<byte> destination, ReadOption option)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = handle.File.Read(out bytesRead, offset, destination, option);
                TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, $", offset: {offset}, size: {destination.Length}");
            }
            else
            {
                rc = handle.File.Read(out bytesRead, offset, destination, option);
            }

            return rc;
        }

        public Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source)
        {
            return WriteFile(handle, offset, source, WriteOption.None);
        }

        public Result WriteFile(FileHandle handle, long offset, ReadOnlySpan<byte> source, WriteOption option)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                TimeSpan startTime = Time.GetCurrent();
                rc = handle.File.Write(offset, source, option);
                TimeSpan endTime = Time.GetCurrent();

                string optionString = (option & WriteOption.Flush) == 0 ? "" : $", write_option: {option}";

                OutputAccessLog(rc, startTime, endTime, handle, $", offset: {offset}, size: {source.Length}{optionString}");
            }
            else
            {
                rc = handle.File.Write(offset, source, option);
            }

            return rc;
        }

        public Result FlushFile(FileHandle handle)
        {
            return RunOperationWithAccessLog(LocalAccessLogMode.All, handle,
                () => handle.File.Flush(),
                () => string.Empty);
        }

        public Result GetFileSize(out long fileSize, FileHandle handle)
        {
            return handle.File.GetSize(out fileSize);
        }

        public Result SetFileSize(FileHandle handle, long size)
        {
            return RunOperationWithAccessLog(LocalAccessLogMode.All, handle,
                () => handle.File.SetSize(size),
                () => $", size: {size}");
        }

        public OpenMode GetFileOpenMode(FileHandle handle)
        {
            return handle.File.OpenMode;
        }

        public void CloseFile(FileHandle handle)
        {
            RunOperationWithAccessLog(LocalAccessLogMode.All, handle,
                () =>
                {
                    handle.File.Dispose();
                    return Result.Success;
                },
                () => string.Empty);
        }
    }
}
