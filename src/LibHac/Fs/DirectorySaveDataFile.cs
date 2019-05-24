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

        public override int Read(Span<byte> destination, long offset)
        {
            return BaseFile.Read(destination, offset);
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            BaseFile.Write(source, offset);
        }

        public override void Flush()
        {
            BaseFile.Flush();
        }

        public override long GetSize()
        {
            return BaseFile.GetSize();
        }

        public override void SetSize(long size)
        {
            BaseFile.SetSize(size);
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
