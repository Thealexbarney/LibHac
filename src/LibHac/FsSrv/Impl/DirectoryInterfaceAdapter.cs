using System;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Sf;
using IDirectory = LibHac.Fs.Fsa.IDirectory;
using IDirectorySf = LibHac.FsSrv.Sf.IDirectory;

namespace LibHac.FsSrv.Impl
{
    public class DirectoryInterfaceAdapter : IDirectorySf
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

        public Result Read(out long entriesRead, OutBuffer entryBuffer)
        {
            const int maxTryCount = 2;
            UnsafeHelpers.SkipParamInit(out entriesRead);

            Span<DirectoryEntry> entries = MemoryMarshal.Cast<byte, DirectoryEntry>(entryBuffer.Buffer);

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
            UnsafeHelpers.SkipParamInit(out entryCount);

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
