using System;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public class DirectorySaveDataFile : FileBase
    {
        private IFile BaseFile { get; }
        private DirectorySaveDataFileSystem ParentFs { get; }
        private OpenMode Mode { get; }

        public DirectorySaveDataFile(DirectorySaveDataFileSystem parentFs, IFile baseFile, OpenMode mode)
        {
            ParentFs = parentFs;
            BaseFile = baseFile;
            Mode = mode;
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, ReadOptionFlag options)
        {
            return BaseFile.Read(out bytesRead, offset, destination, options);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, WriteOptionFlag options)
        {
            return BaseFile.Write(offset, source, options);
        }

        protected override Result DoFlush()
        {
            return BaseFile.Flush();
        }

        protected override Result DoGetSize(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        protected override Result DoSetSize(long size)
        {
            return BaseFile.SetSize(size);
        }

        protected override void Dispose(bool disposing)
        {
            if (Mode.HasFlag(OpenMode.Write))
            {
                ParentFs.NotifyCloseWritableFile();
            }

            BaseFile?.Dispose();
        }
    }
}
