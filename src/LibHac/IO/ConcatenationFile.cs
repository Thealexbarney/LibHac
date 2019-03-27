using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LibHac.IO
{
    public class ConcatenationFile : FileBase
    {
        private IFileSystem BaseFileSystem { get; }
        private string FilePath { get; }
        private List<IFile> Sources { get; }
        private long SubFileSize { get; }

        internal ConcatenationFile(IFileSystem baseFileSystem, string path, IEnumerable<IFile> sources, long subFileSize, OpenMode mode)
        {
            BaseFileSystem = baseFileSystem;
            FilePath = path;
            Sources = sources.ToList();
            SubFileSize = subFileSize;
            Mode = mode;

            for (int i = 0; i < Sources.Count - 1; i++)
            {
                if (Sources[i].GetSize() != SubFileSize)
                {
                    throw new ArgumentException($"Source file must have size {subFileSize}");
                }
            }

            ToDispose.AddRange(Sources);
        }

        public override int Read(Span<byte> destination, long offset)
        {
            long inPos = offset;
            int outPos = 0;
            int remaining = ValidateReadParamsAndGetSize(destination, offset);

            while (remaining > 0)
            {
                int fileIndex = GetSubFileIndexFromOffset(offset);
                IFile file = Sources[fileIndex];
                long fileOffset = offset - fileIndex * SubFileSize;

                long fileEndOffset = Math.Min((fileIndex + 1) * SubFileSize, GetSize());
                int bytesToRead = (int)Math.Min(fileEndOffset - inPos, remaining);
                int bytesRead = file.Read(destination.Slice(outPos, bytesToRead), fileOffset);

                outPos += bytesRead;
                inPos += bytesRead;
                remaining -= bytesRead;

                if (bytesRead < bytesToRead) break;
            }

            return outPos;
        }

        public override void Write(ReadOnlySpan<byte> source, long offset)
        {
            ValidateWriteParams(source, offset);

            int inPos = 0;
            long outPos = offset;
            int remaining = source.Length;

            while (remaining > 0)
            {
                int fileIndex = GetSubFileIndexFromOffset(outPos);
                IFile file = Sources[fileIndex];
                long fileOffset = outPos - fileIndex * SubFileSize;

                long fileEndOffset = Math.Min((fileIndex + 1) * SubFileSize, GetSize());
                int bytesToWrite = (int)Math.Min(fileEndOffset - outPos, remaining);
                file.Write(source.Slice(inPos, bytesToWrite), fileOffset);

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }
        }

        public override void Flush()
        {
            foreach (IFile file in Sources)
            {
                file.Flush();
            }
        }

        public override long GetSize()
        {
            long size = 0;

            foreach (IFile file in Sources)
            {
                size += file.GetSize();
            }

            return size;
        }

        public override void SetSize(long size)
        {
            long currentSize = GetSize();

            if (currentSize == size) return;

            int currentSubFileCount = QuerySubFileCount(currentSize, SubFileSize);
            int newSubFileCount = QuerySubFileCount(size, SubFileSize);

            if (size > currentSize)
            {
                IFile currentLastSubFile = Sources[currentSubFileCount - 1];
                long newSubFileSize = QuerySubFileSize(currentSubFileCount - 1, size, SubFileSize);

                currentLastSubFile.SetSize(newSubFileSize);

                for (int i = currentSubFileCount; i < newSubFileCount; i++)
                {
                    string newSubFilePath = ConcatenationFileSystem.GetSubFilePath(FilePath, i);
                    newSubFileSize = QuerySubFileSize(i, size, SubFileSize);

                    BaseFileSystem.CreateFile(newSubFilePath, newSubFileSize, CreateFileOptions.None);
                    Sources.Add(BaseFileSystem.OpenFile(newSubFilePath, Mode));
                }
            }
            else
            {
                for (int i = currentSubFileCount - 1; i > newSubFileCount - 1; i--)
                {
                    Sources[i].Dispose();
                    Sources.RemoveAt(i);

                    string subFilePath = ConcatenationFileSystem.GetSubFilePath(FilePath, i);
                    BaseFileSystem.DeleteFile(subFilePath);
                }

                long newLastFileSize = QuerySubFileSize(newSubFileCount - 1, size, SubFileSize);
                Sources[newSubFileCount - 1].SetSize(newLastFileSize);
            }
        }

        private int GetSubFileIndexFromOffset(long offset)
        {
            return (int)(offset / SubFileSize);
        }

        private static int QuerySubFileCount(long size, long subFileSize)
        {
            Debug.Assert(size >= 0);
            Debug.Assert(subFileSize > 0);

            if (size == 0) return 1;

            return (int)Util.DivideByRoundUp(size, subFileSize);
        }

        private static long QuerySubFileSize(int subFileIndex, long totalSize, long subFileSize)
        {
            int subFileCount = QuerySubFileCount(totalSize, subFileSize);

            Debug.Assert(subFileIndex < subFileCount);

            if (subFileIndex + 1 == subFileCount)
            {
                long remainder = totalSize % subFileSize;
                return remainder == 0 ? subFileSize : remainder;
            }

            return subFileSize;
        }
    }
}
