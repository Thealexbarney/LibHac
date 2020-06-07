using System;

namespace LibHac.Fs.Fsa
{
    // ReSharper disable once InconsistentNaming
    public abstract class IDirectory2 : IDisposable
    {
        public Result Read(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            if (entryBuffer.IsEmpty)
            {
                entriesRead = 0;
                return Result.Success;
            }

            return DoRead(out entriesRead, entryBuffer);
        }

        public Result GetEntryCount(out long entryCount)
        {
            return DoGetEntryCount(out entryCount);
        }

        protected abstract Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer);
        protected abstract Result DoGetEntryCount(out long entryCount);

        protected virtual void Dispose(bool disposing) { }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }
}
