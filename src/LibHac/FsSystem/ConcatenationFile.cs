using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class ConcatenationFile : IFile
    {
        private IFileSystem BaseFileSystem { get; }
        private U8String FilePath { get; }
        private List<IFile> Sources { get; }
        private long SubFileSize { get; }
        private OpenMode Mode { get; }

        internal ConcatenationFile(IFileSystem baseFileSystem, U8Span path, IEnumerable<IFile> sources, long subFileSize, OpenMode mode)
        {
            BaseFileSystem = baseFileSystem;
            FilePath = path.ToU8String();
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
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
            in ReadOption option)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            long inPos = offset;
            int outPos = 0;

            Result rc = DryRead(out long remaining, offset, destination.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            GetSize(out long fileSize).ThrowIfFailure();

            while (remaining > 0)
            {
                int fileIndex = GetSubFileIndexFromOffset(offset);
                IFile file = Sources[fileIndex];
                long fileOffset = offset - fileIndex * SubFileSize;

                long fileEndOffset = Math.Min((fileIndex + 1) * SubFileSize, fileSize);
                int bytesToRead = (int)Math.Min(fileEndOffset - inPos, remaining);

                rc = file.Read(out long subFileBytesRead, fileOffset, destination.Slice(outPos, bytesToRead), option);
                if (rc.IsFailure()) return rc;

                outPos += (int)subFileBytesRead;
                inPos += subFileBytesRead;
                remaining -= subFileBytesRead;

                if (bytesRead < bytesToRead) break;
            }

            bytesRead = outPos;

            return Result.Success;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            Result rc = DryWrite(out _, offset, source.Length, in option, Mode);
            if (rc.IsFailure()) return rc;

            int inPos = 0;
            long outPos = offset;
            int remaining = source.Length;

            rc = GetSize(out long fileSize);
            if (rc.IsFailure()) return rc;

            while (remaining > 0)
            {
                int fileIndex = GetSubFileIndexFromOffset(outPos);
                IFile file = Sources[fileIndex];
                long fileOffset = outPos - fileIndex * SubFileSize;

                long fileEndOffset = Math.Min((fileIndex + 1) * SubFileSize, fileSize);
                int bytesToWrite = (int)Math.Min(fileEndOffset - outPos, remaining);

                rc = file.Write(fileOffset, source.Slice(inPos, bytesToWrite), option);
                if (rc.IsFailure()) return rc;

                outPos += bytesToWrite;
                inPos += bytesToWrite;
                remaining -= bytesToWrite;
            }

            if (option.HasFlushFlag())
            {
                return Flush();
            }

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            foreach (IFile file in Sources)
            {
                Result rc = file.Flush();
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        protected override Result DoGetSize(out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            foreach (IFile file in Sources)
            {
                Result rc = file.GetSize(out long subFileSize);
                if (rc.IsFailure()) return rc;

                size += subFileSize;
            }

            return Result.Success;
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
        {
            return ResultFs.NotImplemented.Log();
        }

        protected override Result DoSetSize(long size)
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
                    Unsafe.SkipInit(out FsPath newSubFilePath);

                    rc = ConcatenationFileSystem.GetSubFilePath(newSubFilePath.Str, FilePath, i);
                    if (rc.IsFailure()) return rc;

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

                    Unsafe.SkipInit(out FsPath subFilePath);

                    rc = ConcatenationFileSystem.GetSubFilePath(subFilePath.Str, FilePath, i);
                    if (rc.IsFailure()) return rc;

                    rc = BaseFileSystem.DeleteFile(subFilePath);
                    if (rc.IsFailure()) return rc;
                }

                long newLastFileSize = QuerySubFileSize(newSubFileCount - 1, size, SubFileSize);

                rc = Sources[newSubFileCount - 1].SetSize(newLastFileSize);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (IFile file in Sources)
                {
                    file?.Dispose();
                }

                Sources.Clear();
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

            return (int)BitUtil.DivideUp(size, subFileSize);
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
