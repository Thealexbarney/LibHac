using System;
using System.Runtime.InteropServices;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;

namespace LibHac.Fs.Impl
{
    /// <summary>
    /// An adapter for using an <see cref="IDirectorySf"/> service object as an <see cref="IDirectory"/>. Used
    /// when receiving a Horizon IPC directory object so it can be used as an <see cref="IDirectory"/> locally.
    /// </summary>
    internal class DirectoryServiceObjectAdapter : IDirectory
    {
        private ReferenceCountedDisposable<IDirectorySf> BaseDirectory { get; }

        public DirectoryServiceObjectAdapter(ReferenceCountedDisposable<IDirectorySf> baseDirectory)
        {
            BaseDirectory = baseDirectory.AddReference();
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            Span<byte> buffer = MemoryMarshal.Cast<DirectoryEntry, byte>(entryBuffer);
            return BaseDirectory.Target.Read(out entriesRead, buffer);
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            return BaseDirectory.Target.GetEntryCount(out entryCount);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseDirectory?.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}
