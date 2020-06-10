using System;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSystem
{
    public class DirectorySaveDataFile : IFile
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

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            return BaseFile.Read(out bytesRead, offset, destination, in option);
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            return BaseFile.Write(offset, source, in option);
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

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            return BaseFile.OperateRange(outBuffer, operationId, offset, size, inBuffer);
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
