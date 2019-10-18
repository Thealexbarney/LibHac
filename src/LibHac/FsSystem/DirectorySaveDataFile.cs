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

        protected override Result ReadImpl(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            return BaseFile.Read(out bytesRead, offset, destination, options);
        }

        protected override Result WriteImpl(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return BaseFile.Write(offset, source, options);
        }

        protected override Result FlushImpl()
        {
            return BaseFile.Flush();
        }

        protected override Result GetSizeImpl(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        protected override Result SetSizeImpl(long size)
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
