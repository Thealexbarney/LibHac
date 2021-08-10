using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;
using static LibHac.FsSystem.Utility12;

namespace LibHac.FsSystem
{
    /// <summary>
    /// An <see cref="IFileSystem"/> that stores large files as smaller, separate sub-files.
    /// </summary>
    /// <remarks>
    /// This filesystem is mainly used to allow storing large files on filesystems that have low
    /// limits on file size such as FAT filesystems. The underlying base filesystem must have
    /// support for the "Archive" file attribute found in FAT or NTFS filesystems.<br/>
    ///<br/>
    /// A <see cref="ConcatenationFileSystem"/> may contain both standard files or Concatenation files.
    /// If a directory has the archive attribute set, its contents will be concatenated and treated
    /// as a single file. These sub-files must follow the naming scheme "00", "01", "02", ...
    /// Each sub-file except the final one must have the size <see cref="_InternalFileSize"/> that was specified
    /// at the creation of the <see cref="ConcatenationFileSystem"/>.
    /// <br/>Based on FS 12.0.3 (nnSdk 12.3.1)
    /// </remarks>
    public class ConcatenationFileSystem : IFileSystem
    {
        private class ConcatenationFile : IFile
        {
            private OpenMode _mode;
            private List<IFile> _files;
            private long _internalFileSize;
            private IFileSystem _baseFileSystem;
            private Path.Stored _path;

            public ConcatenationFile(OpenMode mode, ref List<IFile> internalFiles, long internalFileSize, IFileSystem baseFileSystem)
            {
                _mode = mode;
                _files = Shared.Move(ref internalFiles);
                _internalFileSize = internalFileSize;
                _baseFileSystem = baseFileSystem;
                _path = new Path.Stored();
            }

            public override void Dispose()
            {
                _path.Dispose();

                foreach (IFile file in _files)
                {
                    file?.Dispose();
                }

                _files.Clear();

                base.Dispose();
            }

            public Result Initialize(in Path path)
            {
                return _path.Initialize(in path);
            }

            private int GetInternalFileIndex(long offset)
            {
                return (int)(offset / _internalFileSize);
            }

            private int GetInternalFileCount(long size)
            {
                if (size == 0)
                    return 1;

                return (int)BitUtil.DivideUp(size, _internalFileSize);
            }

            private long GetInternalFileSize(long offset, int tailIndex)
            {
                int index = GetInternalFileIndex(offset);

                if (tailIndex < index)
                    return _internalFileSize;

                Assert.SdkAssert(index == tailIndex);

                return offset % _internalFileSize;
            }

            protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
                in ReadOption option)
            {
                UnsafeHelpers.SkipParamInit(out bytesRead);

                long fileOffset = offset;
                int bufferOffset = 0;

                Result rc = DryRead(out long remaining, offset, destination.Length, in option, _mode);
                if (rc.IsFailure()) return rc;

                while (remaining > 0)
                {
                    int fileIndex = GetInternalFileIndex(fileOffset);
                    long internalFileRemaining = _internalFileSize - GetInternalFileSize(fileOffset, fileIndex);
                    long internalFileOffset = fileOffset - _internalFileSize * fileIndex;

                    int bytesToRead = (int)Math.Min(remaining, internalFileRemaining);

                    Assert.SdkAssert(fileIndex < _files.Count);

                    rc = _files[fileIndex].Read(out long internalFileBytesRead, internalFileOffset,
                        destination.Slice(bufferOffset, bytesToRead), in option);
                    if (rc.IsFailure()) return rc;

                    remaining -= internalFileBytesRead;
                    bufferOffset += (int)internalFileBytesRead;
                    fileOffset += internalFileBytesRead;

                    if (internalFileBytesRead < bytesToRead)
                        break;
                }

                bytesRead = bufferOffset;
                return Result.Success;
            }

            protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
            {
                Result rc = DryWrite(out bool needsAppend, offset, source.Length, in option, _mode);
                if (rc.IsFailure()) return rc;

                if (source.Length > 0 && needsAppend)
                {
                    rc = SetSize(offset + source.Length);
                    if (rc.IsFailure()) return rc;
                }

                int remaining = source.Length;
                int bufferOffset = 0;
                long fileOffset = offset;

                // No need to send the flush option to the internal files. We'll flush them after all the writes are done.
                var internalFileOption = new WriteOption(option.Flags & ~WriteOptionFlag.Flush);

                while (remaining > 0)
                {
                    int fileIndex = GetInternalFileIndex(fileOffset);
                    long internalFileRemaining = _internalFileSize - GetInternalFileSize(fileOffset, fileIndex);
                    long internalFileOffset = fileOffset - _internalFileSize * fileIndex;

                    int bytesToWrite = (int)Math.Min(remaining, internalFileRemaining);

                    Assert.SdkAssert(fileIndex < _files.Count);

                    rc = _files[fileIndex].Write(internalFileOffset, source.Slice(bufferOffset, bytesToWrite),
                        in internalFileOption);
                    if (rc.IsFailure()) return rc;

                    remaining -= bytesToWrite;
                    bufferOffset += bytesToWrite;
                    fileOffset += bytesToWrite;
                }

                if (option.HasFlushFlag())
                {
                    rc = Flush();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }

            protected override Result DoFlush()
            {
                if (!_mode.HasFlag(OpenMode.Write))
                    return Result.Success;

                foreach (IFile file in _files)
                {
                    Result rc = file.Flush();
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }

            protected override Result DoSetSize(long size)
            {
                Result rc = DrySetSize(size, _mode);
                if (rc.IsFailure()) return rc;

                rc = GetSize(out long currentSize);
                if (rc.IsFailure()) return rc;

                if (currentSize == size) return Result.Success;

                int currentTailIndex = GetInternalFileCount(currentSize) - 1;
                int newTailIndex = GetInternalFileCount(size) - 1;

                using var internalFilePath = new Path();
                rc = internalFilePath.Initialize(in _path);
                if (rc.IsFailure()) return rc;

                if (size > currentSize)
                {
                    rc = _files[currentTailIndex].SetSize(GetInternalFileSize(size, currentTailIndex));
                    if (rc.IsFailure()) return rc;

                    for (int i = currentTailIndex + 1; i < newTailIndex; i++)
                    {
                        rc = AppendInternalFilePath(ref internalFilePath.Ref(), i);
                        if (rc.IsFailure()) return rc;

                        rc = _baseFileSystem.CreateFile(in internalFilePath, GetInternalFileSize(size, i),
                            CreateFileOptions.None);
                        if (rc.IsFailure()) return rc;

                        using var newInternalFile = new UniqueRef<IFile>();
                        rc = _baseFileSystem.OpenFile(ref newInternalFile.Ref(), in internalFilePath, _mode);
                        if (rc.IsFailure()) return rc;

                        _files.Add(newInternalFile.Release());

                        rc = internalFilePath.RemoveChild();
                        if (rc.IsFailure()) return rc;
                    }
                }
                else
                {
                    for (int i = currentTailIndex - 1; i > newTailIndex; i--)
                    {
                        _files[i].Dispose();
                        _files.RemoveAt(i);

                        rc = AppendInternalFilePath(ref internalFilePath.Ref(), i);
                        if (rc.IsFailure()) return rc;

                        rc = _baseFileSystem.DeleteFile(in internalFilePath);
                        if (rc.IsFailure()) return rc;

                        rc = internalFilePath.RemoveChild();
                        if (rc.IsFailure()) return rc;
                    }

                    rc = _files[newTailIndex].SetSize(GetInternalFileSize(size, newTailIndex));
                    if (rc.IsFailure()) return rc;
                }

                return Result.Success;
            }

            protected override Result DoGetSize(out long size)
            {
                UnsafeHelpers.SkipParamInit(out size);

                long totalSize = 0;

                foreach (IFile file in _files)
                {
                    Result rc = file.GetSize(out long internalFileSize);
                    if (rc.IsFailure()) return rc;

                    totalSize += internalFileSize;
                }

                size = totalSize;
                return Result.Success;
            }

            protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
                ReadOnlySpan<byte> inBuffer)
            {
                if (operationId == OperationId.InvalidateCache)
                {
                    if (!_mode.HasFlag(OpenMode.Read))
                        return ResultFs.ReadUnpermitted.Log();

                    var closure = new OperateRangeClosure();
                    closure.OutBuffer = outBuffer;
                    closure.InBuffer = inBuffer;
                    closure.OperationId = operationId;

                    Result rc = DoOperateRangeImpl(offset, size, InvalidateCacheImpl, ref closure);
                    if (rc.IsFailure()) return rc;
                }
                else if (operationId == OperationId.QueryRange)
                {
                    if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                        return ResultFs.InvalidSize.Log();

                    var closure = new OperateRangeClosure();
                    closure.InBuffer = inBuffer;
                    closure.OperationId = operationId;
                    closure.InfoMerged.Clear();

                    Result rc = DoOperateRangeImpl(offset, size, QueryRangeImpl, ref closure);
                    if (rc.IsFailure()) return rc;

                    SpanHelpers.AsByteSpan(ref closure.InfoMerged).CopyTo(outBuffer);
                }
                else
                {
                    return ResultFs.UnsupportedOperateRangeForConcatenationFile.Log();
                }

                return Result.Success;

                static Result InvalidateCacheImpl(IFile file, long offset, long size, ref OperateRangeClosure closure)
                {
                    return file.OperateRange(closure.OutBuffer, closure.OperationId, offset, size, closure.InBuffer);
                }

                static Result QueryRangeImpl(IFile file, long offset, long size, ref OperateRangeClosure closure)
                {
                    Unsafe.SkipInit(out QueryRangeInfo infoEntry);

                    Result rc = file.OperateRange(SpanHelpers.AsByteSpan(ref infoEntry), closure.OperationId, offset, size,
                        closure.InBuffer);
                    if (rc.IsFailure()) return rc;

                    closure.InfoMerged.Merge(in infoEntry);
                    return Result.Success;
                }
            }

            private Result DoOperateRangeImpl(long offset, long size, OperateRangeTask func,
                ref OperateRangeClosure closure)
            {
                if (offset < 0)
                    return ResultFs.OutOfRange.Log();

                Result rc = GetSize(out long currentSize);
                if (rc.IsFailure()) return rc;

                if (offset > currentSize)
                    return ResultFs.OutOfRange.Log();

                long currentOffset = offset;
                long availableSize = currentSize - offset;
                long remaining = Math.Min(size, availableSize);

                while (remaining > 0)
                {
                    int fileIndex = GetInternalFileIndex(currentOffset);
                    long internalFileRemaining = _internalFileSize - GetInternalFileSize(currentOffset, fileIndex);
                    long internalFileOffset = currentOffset - _internalFileSize * fileIndex;

                    long sizeToOperate = Math.Min(remaining, internalFileRemaining);

                    Assert.SdkAssert(fileIndex < _files.Count);

                    rc = func(_files[fileIndex], internalFileOffset, sizeToOperate, ref closure);
                    if (rc.IsFailure()) return rc;

                    remaining -= sizeToOperate;
                    currentOffset += sizeToOperate;
                }

                return Result.Success;
            }

            private delegate Result OperateRangeTask(IFile file, long offset, long size, ref OperateRangeClosure closure);

            private ref struct OperateRangeClosure
            {
                public Span<byte> OutBuffer;
                public ReadOnlySpan<byte> InBuffer;
                public OperationId OperationId;
                public QueryRangeInfo InfoMerged;
            }
        }

        private class ConcatenationDirectory : IDirectory
        {
            private OpenDirectoryMode _mode;
            private UniqueRef<IDirectory> _baseDirectory;
            private Path.Stored _path;
            private IFileSystem _baseFileSystem;
            private ConcatenationFileSystem _concatenationFileSystem;

            public ConcatenationDirectory(OpenDirectoryMode mode, ref UniqueRef<IDirectory> baseDirectory,
                ConcatenationFileSystem concatFileSystem, IFileSystem baseFileSystem)
            {
                _mode = mode;
                _baseDirectory = new UniqueRef<IDirectory>(ref baseDirectory);
                _baseFileSystem = baseFileSystem;
                _concatenationFileSystem = concatFileSystem;
            }

            public override void Dispose()
            {
                _path.Dispose();
                _baseDirectory.Dispose();

                base.Dispose();
            }

            public Result Initialize(in Path path)
            {
                Result rc = _path.Initialize(in path);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }

            protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
            {
                UnsafeHelpers.SkipParamInit(out entriesRead);

                Unsafe.SkipInit(out DirectoryEntry entry);
                int readCountTotal = 0;

                while (readCountTotal < entryBuffer.Length)
                {
                    Result rc = _baseDirectory.Get.Read(out long readCount, SpanHelpers.AsSpan(ref entry));
                    if (rc.IsFailure()) return rc;

                    if (readCount == 0)
                        break;

                    if (!IsReadTarget(in entry))
                        continue;

                    if (IsConcatenationFileAttribute(entry.Attributes))
                    {
                        entry.Type = DirectoryEntryType.File;

                        if (!_mode.HasFlag(OpenDirectoryMode.NoFileSize))
                        {
                            using var internalFilePath = new Path();
                            rc = internalFilePath.Initialize(in _path);
                            if (rc.IsFailure()) return rc;

                            rc = internalFilePath.AppendChild(entry.Name);
                            if (rc.IsFailure()) return rc;

                            rc = _concatenationFileSystem.GetFileSize(out entry.Size, in internalFilePath);
                            if (rc.IsFailure()) return rc;
                        }
                    }

                    entry.Attributes = NxFileAttributes.None;
                    entryBuffer[readCountTotal++] = entry;
                }

                entriesRead = readCountTotal;
                return Result.Success;
            }

            protected override Result DoGetEntryCount(out long entryCount)
            {
                UnsafeHelpers.SkipParamInit(out entryCount);

                Unsafe.SkipInit(out DirectoryEntry entry);
                using var directory = new UniqueRef<IDirectory>();

                Path path = _path.DangerousGetPath();

                Result rc = _baseFileSystem.OpenDirectory(ref directory.Ref(), in path,
                    OpenDirectoryMode.All | OpenDirectoryMode.NoFileSize);
                if (rc.IsFailure()) return rc;

                long entryCountTotal = 0;

                while (true)
                {
                    directory.Get.Read(out long readCount, SpanHelpers.AsSpan(ref entry));
                    if (rc.IsFailure()) return rc;

                    if (readCount == 0)
                        break;

                    if (IsReadTarget(in entry))
                        entryCountTotal++;
                }

                entryCount = entryCountTotal;
                return Result.Success;
            }

            private bool IsReadTarget(in DirectoryEntry entry)
            {
                bool hasConcatAttribute = IsConcatenationFileAttribute(entry.Attributes);

                return _mode.HasFlag(OpenDirectoryMode.File) && (entry.Type == DirectoryEntryType.File || hasConcatAttribute) ||
                       _mode.HasFlag(OpenDirectoryMode.Directory) && entry.Type == DirectoryEntryType.Directory && !hasConcatAttribute;
            }
        }

        public static readonly long DefaultInternalFileSize = 0xFFFF0000; // Hard-coded value used by FS

        private IAttributeFileSystem _baseFileSystem;
        private long _InternalFileSize;

        /// <summary>
        /// Initializes a new <see cref="ConcatenationFileSystem"/> with an internal file size of <see cref="DefaultInternalFileSize"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IAttributeFileSystem"/> for the
        /// new <see cref="ConcatenationFileSystem"/>.</param>
        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem) : this(baseFileSystem, DefaultInternalFileSize) { }

        /// <summary>
        /// Initializes a new <see cref="ConcatenationFileSystem"/>.
        /// </summary>
        /// <param name="baseFileSystem">The base <see cref="IAttributeFileSystem"/> for the
        /// new <see cref="ConcatenationFileSystem"/>.</param>
        /// <param name="internalFileSize">The size of each internal file. Once a file exceeds this size, a new internal file will be created</param>
        public ConcatenationFileSystem(IAttributeFileSystem baseFileSystem, long internalFileSize)
        {
            _baseFileSystem = baseFileSystem;
            _InternalFileSize = internalFileSize;
        }

        public override void Dispose()
        {
            _baseFileSystem?.Dispose();
            _baseFileSystem = null;

            base.Dispose();
        }

        private static ReadOnlySpan<byte> RootPath => new[] { (byte)'/' };

        private static Result AppendInternalFilePath(ref Path path, int index)
        {
            // Use an int as the buffer instead of a stackalloc byte[3] to workaround CS8350.
            // Path.AppendChild will not save the span passed to it so this should be safe.
            int bufferInt = 0;
            Utf8Formatter.TryFormat(index, SpanHelpers.AsByteSpan(ref bufferInt), out _, new StandardFormat('d', 2));

            return path.AppendChild(SpanHelpers.AsByteSpan(ref bufferInt));
        }

        private static Result GenerateInternalFilePath(ref Path outPath, int index, in Path basePath)
        {
            Result rc = outPath.Initialize(in basePath);
            if (rc.IsFailure()) return rc;

            rc = AppendInternalFilePath(ref outPath, index);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private static Result GenerateParentPath(ref Path outParentPath, in Path path)
        {
            if (path == RootPath)
                return ResultFs.PathNotFound.Log();

            Result rc = outParentPath.Initialize(in path);
            if (rc.IsFailure()) return rc;

            rc = outParentPath.RemoveChild();
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        private static bool IsConcatenationFileAttribute(NxFileAttributes attribute)
        {
            return attribute.HasFlag(NxFileAttributes.Directory | NxFileAttributes.Archive);
        }

        private bool IsConcatenationFile(in Path path)
        {
            Result rc = _baseFileSystem.GetFileAttributes(out NxFileAttributes attribute, in path);
            if (rc.IsFailure())
                return false;

            return IsConcatenationFileAttribute(attribute);
        }

        private Result GetInternalFileCount(out int count, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out count);

            using var internalFilePath = new Path();
            Result rc = internalFilePath.Initialize(in path);
            if (rc.IsFailure()) return rc;

            for (int i = 0; ; i++)
            {
                rc = AppendInternalFilePath(ref internalFilePath.Ref(), i);
                if (rc.IsFailure()) return rc;

                rc = _baseFileSystem.GetEntryType(out _, in internalFilePath);
                if (rc.IsFailure())
                {
                    // We've passed the last internal file of the concatenation file
                    // once the next internal file doesn't exist.
                    if (ResultFs.PathNotFound.Includes(rc))
                    {
                        count = i;
                        return Result.Success;
                    }

                    return rc;
                }

                rc = internalFilePath.RemoveChild();
                if (rc.IsFailure()) return rc;
            }
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
        {
            if (IsConcatenationFile(in path))
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            return _baseFileSystem.GetEntryType(out entryType, path);
        }

        protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
        {
            return _baseFileSystem.GetFreeSpaceSize(out freeSpace, path);
        }

        protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
        {
            return _baseFileSystem.GetTotalSpaceSize(out totalSpace, path);
        }

        protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
        {
            return _baseFileSystem.GetFileTimeStampRaw(out timeStamp, path);
        }

        protected override Result DoFlush()
        {
            return _baseFileSystem.Flush();
        }

        protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
        {
            if (!IsConcatenationFile(in path))
            {
                return _baseFileSystem.OpenFile(ref outFile, in path, mode);
            }

            Result rc = GetInternalFileCount(out int fileCount, in path);
            if (rc.IsFailure()) return rc;

            var internalFiles = new List<IFile>(fileCount);

            using var filePath = new Path();
            filePath.Initialize(in path);
            if (rc.IsFailure()) return rc;

            try
            {
                for (int i = 0; i < fileCount; i++)
                {
                    rc = AppendInternalFilePath(ref filePath.Ref(), i);
                    if (rc.IsFailure()) return rc;

                    using var internalFile = new UniqueRef<IFile>();
                    rc = _baseFileSystem.OpenFile(ref internalFile.Ref(), in filePath, mode);
                    if (rc.IsFailure()) return rc;

                    internalFiles.Add(internalFile.Release());

                    rc = filePath.RemoveChild();
                    if (rc.IsFailure()) return rc;
                }

                using var concatFile = new UniqueRef<ConcatenationFile>(
                    new ConcatenationFile(mode, ref internalFiles, _InternalFileSize, _baseFileSystem));

                rc = concatFile.Get.Initialize(in path);
                if (rc.IsFailure()) return rc;

                outFile.Set(ref concatFile.Ref());
                return Result.Success;
            }
            finally
            {
                if (internalFiles is not null)
                {
                    foreach (IFile internalFile in internalFiles)
                    {
                        internalFile?.Dispose();
                    }
                }
            }
        }

        protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
            OpenDirectoryMode mode)
        {
            if (IsConcatenationFile(path))
            {
                return ResultFs.PathNotFound.Log();
            }

            using var baseDirectory = new UniqueRef<IDirectory>();
            Result rc = _baseFileSystem.OpenDirectory(ref baseDirectory.Ref(), path, OpenDirectoryMode.All);
            if (rc.IsFailure()) return rc;

            using var concatDirectory = new UniqueRef<ConcatenationDirectory>(
                new ConcatenationDirectory(mode, ref baseDirectory.Ref(), this, _baseFileSystem));
            rc = concatDirectory.Get.Initialize(in path);
            if (rc.IsFailure()) return rc;

            outDirectory.Set(ref concatDirectory.Ref());
            return Result.Success;
        }

        protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
        {
            CreateFileOptions newOption = option & ~CreateFileOptions.CreateConcatenationFile;

            // Create a normal file if the concatenation file flag isn't set
            if (!option.HasFlag(CreateFileOptions.CreateConcatenationFile))
            {
                return _baseFileSystem.CreateFile(path, size, newOption);
            }

            using var parentPath = new Path();
            Result rc = GenerateParentPath(ref parentPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            if (IsConcatenationFile(in parentPath))
            {
                // Cannot create a file inside of a concatenation file
                return ResultFs.PathNotFound.Log();
            }

            rc = _baseFileSystem.CreateDirectory(in path, NxFileAttributes.Archive);
            if (rc.IsFailure()) return rc;

            // Handle the empty file case by manually creating a single empty internal file
            if (size == 0)
            {
                using var emptyFilePath = new Path();
                rc = GenerateInternalFilePath(ref emptyFilePath.Ref(), 0, in path);
                if (rc.IsFailure()) return rc;

                rc = _baseFileSystem.CreateFile(in emptyFilePath, 0, newOption);
                if (rc.IsFailure()) return rc;

                return Result.Success;
            }

            long remaining = size;
            using var filePath = new Path();
            filePath.Initialize(in path);
            if (rc.IsFailure()) return rc;

            for (int i = 0; remaining > 0; i++)
            {
                rc = AppendInternalFilePath(ref filePath.Ref(), i);
                if (rc.IsFailure()) return rc;

                long fileSize = Math.Min(remaining, _InternalFileSize);
                Result createInternalFileResult = _baseFileSystem.CreateFile(in filePath, fileSize, newOption);

                // If something goes wrong when creating an internal file, delete all the
                // internal files we've created so far and delete the directory.
                // This will allow results like insufficient space results to be returned properly.
                if (createInternalFileResult.IsFailure())
                {
                    for (int index = i - 1; index >= 0; index--)
                    {
                        rc = GenerateInternalFilePath(ref filePath.Ref(), index, in path);
                        if (rc.IsFailure()) return rc;

                        rc = _baseFileSystem.DeleteFile(in filePath);

                        if (rc.IsFailure())
                            break;
                    }

                    _baseFileSystem.DeleteDirectoryRecursively(in path).IgnoreResult();
                    return createInternalFileResult;
                }

                rc = filePath.RemoveChild();
                if (rc.IsFailure()) return rc;

                remaining -= fileSize;
            }

            return Result.Success;
        }

        protected override Result DoDeleteFile(in Path path)
        {
            if (!IsConcatenationFile(in path))
            {
                return _baseFileSystem.DeleteFile(in path);
            }

            Result rc = GetInternalFileCount(out int count, path);
            if (rc.IsFailure()) return rc;

            using var filePath = new Path();
            rc = filePath.Initialize(in path);
            if (rc.IsFailure()) return rc;

            for (int i = count - 1; i >= 0; i--)
            {
                rc = AppendInternalFilePath(ref filePath.Ref(), i);
                if (rc.IsFailure()) return rc;

                rc = _baseFileSystem.DeleteFile(in filePath);
                if (rc.IsFailure()) return rc;

                rc = filePath.RemoveChild();
                if (rc.IsFailure()) return rc;
            }

            rc = _baseFileSystem.DeleteDirectoryRecursively(in path);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoCreateDirectory(in Path path)
        {
            // Check if the parent path is a concatenation file because we can't create a directory inside one.
            using var parentPath = new Path();
            Result rc = GenerateParentPath(ref parentPath.Ref(), in path);
            if (rc.IsFailure()) return rc;

            if (IsConcatenationFile(in parentPath))
                return ResultFs.PathNotFound.Log();

            rc = _baseFileSystem.CreateDirectory(in path);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoDeleteDirectory(in Path path)
        {
            // Make sure the directory isn't a concatenation file.
            if (IsConcatenationFile(path))
                return ResultFs.PathNotFound.Log();

            return _baseFileSystem.DeleteDirectory(path);
        }

        private Result CleanDirectoryRecursivelyImpl(in Path path)
        {
            static Result OnEnterDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
                Result.Success;

            static Result OnExitDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
                closure.SourceFileSystem.DeleteDirectory(in path);

            static Result OnFile(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
                closure.SourceFileSystem.DeleteFile(in path);

            var closure = new FsIterationTaskClosure();
            closure.SourceFileSystem = this;

            var directoryEntry = new DirectoryEntry();
            return CleanupDirectoryRecursively(this, in path, ref directoryEntry, OnEnterDir, OnExitDir, OnFile,
                ref closure);
        }

        protected override Result DoDeleteDirectoryRecursively(in Path path)
        {
            if (IsConcatenationFile(in path))
                return ResultFs.PathNotFound.Log();

            Result rc = CleanDirectoryRecursivelyImpl(in path);
            if (rc.IsFailure()) return rc;

            rc = _baseFileSystem.DeleteDirectory(in path);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoCleanDirectoryRecursively(in Path path)
        {
            if (IsConcatenationFile(in path))
                return ResultFs.PathNotFound.Log();

            Result rc = CleanDirectoryRecursivelyImpl(in path);
            if (rc.IsFailure()) return rc;

            return Result.Success;
        }

        protected override Result DoRenameFile(in Path currentPath, in Path newPath)
        {
            if (IsConcatenationFile(in currentPath))
            {
                return _baseFileSystem.RenameDirectory(in currentPath, in newPath);
            }

            return _baseFileSystem.RenameFile(in currentPath, in newPath);
        }

        protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
        {
            if (IsConcatenationFile(in currentPath))
                return ResultFs.PathNotFound.Log();

            return _baseFileSystem.RenameDirectory(in currentPath, in newPath);
        }

        public Result GetFileSize(out long size, in Path path)
        {
            UnsafeHelpers.SkipParamInit(out size);

            using var internalFilePath = new Path();
            Result rc = internalFilePath.Initialize(in path);
            if (rc.IsFailure()) return rc;

            long sizeTotal = 0;

            for (int i = 0; ; i++)
            {
                rc = AppendInternalFilePath(ref internalFilePath.Ref(), i);
                if (rc.IsFailure()) return rc;

                rc = _baseFileSystem.GetFileSize(out long internalFileSize, in internalFilePath);
                if (rc.IsFailure())
                {
                    // We've passed the last internal file of the concatenation file
                    // once the next internal file doesn't exist.
                    if (ResultFs.PathNotFound.Includes(rc))
                    {
                        size = sizeTotal;
                        return Result.Success;
                    }

                    return rc;
                }

                rc = internalFilePath.RemoveChild();
                if (rc.IsFailure()) return rc;

                sizeTotal += internalFileSize;
            }
        }

        protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
            in Path path)
        {
            if (queryId != QueryId.SetConcatenationFileAttribute)
                return ResultFs.UnsupportedQueryEntryForConcatenationFileSystem.Log();

            return _baseFileSystem.SetFileAttributes(in path, NxFileAttributes.Archive);
        }

        protected override Result DoCommit()
        {
            return _baseFileSystem.Commit();
        }

        protected override Result DoCommitProvisionally(long counter)
        {
            return _baseFileSystem.CommitProvisionally(counter);
        }
    }
}
