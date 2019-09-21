using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace LibHac.FsSystem
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
                Sources[i].GetSize(out long actualSubFileSize).ThrowIfFailure();

                if (actualSubFileSize != SubFileSize)
                {
                    throw new ArgumentException($"Source file must have size {subFileSize}");
                }
            }

            ToDispose.AddRange(Sources);
        }

        public override Result Read(out long bytesRead, long offset, Span<byte> destination, ReadOption options)
        {
            bytesRead = default;

            long inPos = offset;
            int outPos = 0;
            int remaining = ValidateReadParamsAndGetSize(destination, offset);

            GetSize(out long fileSize).ThrowIfFailure();

            while (remaining > 0)
            {
                int fileIndex = GetSubFileIndexFromOffset(offset);
                IFile file = Sources[fileIndex];
                long fileOffset = offset - fileIndex * SubFileSize;

                long fileEndOffset = Math.Min((fileIndex + 1) * SubFileSize, fileSize);
                int bytesToRead = (int)Math.Min(fileEndOffset - inPos, remaining);

                Result rc = file.Read(out long subFileBytesRead, fileOffset, destination.Slice(outPos, bytesToRead), options);
                if (rc.IsFailure()) return rc;

                outPos += (int)subFileBytesRead;
                inPos += subFileBytesRead;
                remaining -= (int)subFileBytesRead;

                if (bytesRead < bytesToRead) break;
            }

            bytesRead = outPos;

            return Result.Success;
        }

        public override Result Write(long offset, ReadOnlySpan<byte> source, WriteOption options)
        {
            ValidateWriteParams(source, offset);

            int inPos = 0;
            long outPos = offset;
            int remaining = source.Length;

            GetSize(out long fileSize).ThrowIfFailure();

            while (remaining > 0)
            {
                int fileIndex = GetSubFileIndexFromOffset(outPos);
                IFile file = Sources[fileIndex];
                long fileOffset = outPos - fileIndex * SubFileSize;

                long fileEndOffset = Math.Min((fileIndex + 1) * SubFileSize, fileSize);
                int bytesToWrite = (int)Math.Min(fileEndOffset - outPos, remaining);

                Result rc = file.Write(fileOffset, source.Slice(inPos, bytesToWrite), options);
                if (rc.IsFailure()) return rc;

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }

            if ((options & WriteOption.Flush) != 0)
            {
                return Flush();
            }

            return Result.Success;
        }

        public override Result Flush()
        {
            foreach (IFile file in Sources)
            {
                Result rc = file.Flush();
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        public override Result GetSize(out long size)
        {
            size = default;

            foreach (IFile file in Sources)
            {
                Result rc = file.GetSize(out long subFileSize);
                if (rc.IsFailure()) return rc;

                size += subFileSize;
            }

            return Result.Success;
        }

        public override Result SetSize(long size)
        {
            Result rc = GetSize(out long currentSize);
            if (rc.IsFailure()) return rc;

            if (currentSize == size) return Result.Success;

            int currentSubFileCount = QuerySubFileCount(currentSize, SubFileSize);
            int newSubFileCount = QuerySubFileCount(size, SubFileSize);

            if (size > currentSize)
            {
                IFile currentLastSubFile = Sources[currentSubFileCount - 1];
                long newSubFileSize = QuerySubFileSize(currentSubFileCount - 1, size, SubFileSize);

                rc = currentLastSubFile.SetSize(newSubFileSize);
                if (rc.IsFailure()) return rc;

                for (int i = currentSubFileCount; i < newSubFileCount; i++)
                {
                    string newSubFilePath = ConcatenationFileSystem.GetSubFilePath(FilePath, i);
                    newSubFileSize = QuerySubFileSize(i, size, SubFileSize);

                    rc = BaseFileSystem.CreateFile(newSubFilePath, newSubFileSize, CreateFileOptions.None);
                    if (rc.IsFailure()) return rc;

                    rc = BaseFileSystem.OpenFile(out IFile newSubFile, newSubFilePath, Mode);
                    if (rc.IsFailure()) return rc;

                    Sources.Add(newSubFile);
                }
            }
            else
            {
                for (int i = currentSubFileCount - 1; i > newSubFileCount - 1; i--)
                {
                    Sources[i].Dispose();
                    Sources.RemoveAt(i);

                    string subFilePath = ConcatenationFileSystem.GetSubFilePath(FilePath, i);

                    rc = BaseFileSystem.DeleteFile(subFilePath);
                    if (rc.IsFailure()) return rc;
                }

                long newLastFileSize = QuerySubFileSize(newSubFileCount - 1, size, SubFileSize);

                rc = Sources[newSubFileCount - 1].SetSize(newLastFileSize);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
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
