using System;

namespace LibHac.Fs
{
    public class DirectorySaveDataFile : FileBase
    {
        private IFile BaseFile { get; }
        private DirectorySaveDataFileSystem ParentFs { get; }
        private object DisposeLocker { get; } = new object();

        public DirectorySaveDataFile(DirectorySaveDataFileSystem parentFs, IFile baseFile)
        {
            ParentFs = parentFs;
            BaseFile = baseFile;
            Mode = BaseFile.Mode;
            ToDispose.Add(BaseFile);
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            return BaseFile.Read(out bytesRead, offset, destination, options);
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            return BaseFile.Write(offset, source, options);
        }

        public override Result Flush()
        {
            return BaseFile.Flush();
        }

        public override Result GetSize(out long size)
        {
            return BaseFile.GetSize(out size);
        }

        public override Result SetSize(long size)
        {
            return BaseFile.SetSize(size);
        }

        protected override void Dispose(bool disposing)
        {
            lock (DisposeLocker)
            {
                if (IsDisposed) return;

                base.Dispose(disposing);

                if (Mode.HasFlag(OpenMode.Write))
                {
                    ParentFs.NotifyCloseWritableFile();
                }
            }
        }
    }
}
