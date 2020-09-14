using System;
using System.Runtime.InteropServices;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv.Sf;

namespace LibHac.FsSrv.Impl
{
    internal class DirectoryInterfaceAdapter : IDirectorySf
    {
        private ReferenceCountedDisposable<FileSystemInterfaceAdapter> ParentFs { get; }
        private IDirectory BaseDirectory { get; }

        public DirectoryInterfaceAdapter(IDirectory baseDirectory,
            ref ReferenceCountedDisposable<FileSystemInterfaceAdapter> parentFileSystem)
        {
            BaseDirectory = baseDirectory;
            ParentFs = parentFileSystem;
            parentFileSystem = null;
        }

        public Result Read(out long entriesRead, Span<byte> entryBuffer)
        {
            const int maxTryCount = 2;
            entriesRead = default;

            Span<DirectoryEntry> entries = MemoryMarshal.Cast<byte, DirectoryEntry>(entryBuffer);

            Result rc = Result.Success;
            long tmpEntriesRead = 0;

            for (int tryNum = 0; tryNum < maxTryCount; tryNum++)
            {
                rc = BaseDirectory.Read(out tmpEntriesRead, entries);

                // Retry on ResultDataCorrupted
                if (!ResultFs.DataCorrupted.Includes(rc))
                    break;
            }

            if (rc.IsFailure()) return rc;

            entriesRead = tmpEntriesRead;
            return Result.Success;
        }

        public Result GetEntryCount(out long entryCount)
        {
            entryCount = default;

            Result rc = BaseDirectory.GetEntryCount(out long tmpEntryCount);
            if (rc.IsFailure()) return rc;

            entryCount = tmpEntryCount;
            return Result.Success;
        }

        public void Dispose()
        {
            BaseDirectory?.Dispose();
            ParentFs?.Dispose();
        }
    }
}
