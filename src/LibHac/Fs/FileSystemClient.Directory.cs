using System;

namespace LibHac.Fs
{
    public partial class FileSystemClient
    {
        public Result GetDirectoryEntryCount(out long count, DirectoryHandle handle)
        {
            return handle.Directory.GetEntryCount(out count);
        }

        public Result ReadDirectory(out long entriesRead, Span<DirectoryEntry> entryBuffer, DirectoryHandle handle)
        {
            Result rc;

            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                System.TimeSpan startTime = Time.GetCurrent();
                rc = handle.Directory.Read(out entriesRead, entryBuffer);
                System.TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(rc, startTime, endTime, handle, string.Empty);
            }
            else
            {
                rc = handle.Directory.Read(out entriesRead, entryBuffer);
            }

            return rc;
        }

        public void CloseDirectory(DirectoryHandle handle)
        {
            if (IsEnabledAccessLog() && IsEnabledHandleAccessLog(handle))
            {
                System.TimeSpan startTime = Time.GetCurrent();
                handle.Directory.Dispose();
                System.TimeSpan endTime = Time.GetCurrent();

                OutputAccessLog(Result.Success, startTime, endTime, handle, string.Empty);
            }
            else
            {
                handle.Directory.Dispose();
            }
        }
    }
}
