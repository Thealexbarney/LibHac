using System;
using System.IO;

namespace LibHac.IO
{
    public class LocalFile : FileBase
    {
        private string Path { get; }
        private FileStream Stream { get; }
        private StreamFile File { get; }

        public LocalFile(string path, OpenMode mode)
        {
            Path = path;
            Mode = mode;
            Stream = new FileStream(Path, FileMode.Open, GetFileAccess(mode));
            File = new StreamFile(Stream, mode);

            ToDispose.Add(File);
            ToDispose.Add(Stream);
        }

        public override int Read(Span<byte> destination, long offset)
        {
            int toRead = ValidateReadParamsAndGetSize(destination, offset);

            File.Read(destination.Slice(0, toRead), offset);

            return toRead;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateWriteParams(source, offset);
            
            File.Write(source, offset);
        }

        public override void Flush()
        {
            File.Flush();
        }

        public override long GetSize()
        {
            return File.GetSize();
        }

        public override void SetSize(long size)
        {
            File.SetSize(size);
        }

        private static FileAccess GetFileAccess(OpenMode mode)
        {
            // FileAccess and OpenMode have the same flags
            return (FileAccess)(mode & OpenMode.ReadWrite);
        }
    }
}
