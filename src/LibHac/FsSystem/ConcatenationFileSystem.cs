using System;
using System.Buffers;
using System.Buffers.Text;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Common.FixedArrays;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Os;
using LibHac.Util;
using static LibHac.FsSystem.Utility;

namespace LibHac.FsSystem;

/// <summary>
/// An <see cref="IFileSystem"/> that stores large files as smaller, separate sub-files.
/// </summary>
/// <remarks>
/// <para>This filesystem is mainly used to allow storing large files on filesystems that have low
/// limits on file size such as FAT filesystems. The underlying base filesystem must have
/// support for the "Archive" file attribute found in FAT or NTFS filesystems.
/// </para> 
/// <para>A <see cref="ConcatenationFileSystem"/> may contain both standard files or Concatenation files.
/// If a directory has the archive attribute set, its contents will be concatenated and treated
/// as a single file. These internal files must follow the naming scheme "00", "01", "02", ...
/// Each internal file except the final one must have the internal file size that was specified
/// at the creation of the <see cref="ConcatenationFileSystem"/>.
/// </para>
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para>
/// </remarks>
public class ConcatenationFileSystem : IFileSystem
{
    private class ConcatenationFile : IFile
    {
        private OpenMode _mode;
        private List<IFile> _fileArray;
        private long _internalFileSize;
        private IFileSystem _baseFileSystem;
        private Path.Stored _path;

        public ConcatenationFile(OpenMode mode, ref List<IFile> internalFiles, long internalFileSize, IFileSystem baseFileSystem)
        {
            _mode = mode;
            _fileArray = Shared.Move(ref internalFiles);
            _internalFileSize = internalFileSize;
            _baseFileSystem = baseFileSystem;
            _path = new Path.Stored();
        }

        public override void Dispose()
        {
            _path.Dispose();

            foreach (IFile file in _fileArray)
            {
                file?.Dispose();
            }

            _fileArray.Clear();

            base.Dispose();
        }

        public Result Initialize(in Path path)
        {
            return _path.Initialize(in path).Ret();
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

            Result res = DryRead(out long remaining, offset, destination.Length, in option, _mode);
            if (res.IsFailure()) return res.Miss();

            int bufferOffset = 0;

            while (remaining > 0)
            {
                int fileIndex = GetInternalFileIndex(fileOffset);
                long internalFileRemaining = _internalFileSize - GetInternalFileSize(fileOffset, fileIndex);
                long internalFileOffset = fileOffset - _internalFileSize * fileIndex;

                int bytesToRead = (int)Math.Min(remaining, internalFileRemaining);

                Assert.SdkAssert(fileIndex < _fileArray.Count);

                res = _fileArray[fileIndex].Read(out long internalFileBytesRead, internalFileOffset,
                    destination.Slice(bufferOffset, bytesToRead), in option);
                if (res.IsFailure()) return res.Miss();

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
            Result res = DryWrite(out bool needsAppend, offset, source.Length, in option, _mode);
            if (res.IsFailure()) return res.Miss();

            if (source.Length > 0 && needsAppend)
            {
                res = SetSize(offset + source.Length);
                if (res.IsFailure()) return res.Miss();
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

                Assert.SdkAssert(fileIndex < _fileArray.Count);

                res = _fileArray[fileIndex].Write(internalFileOffset, source.Slice(bufferOffset, bytesToWrite),
                    in internalFileOption);
                if (res.IsFailure()) return res.Miss();

                remaining -= bytesToWrite;
                bufferOffset += bytesToWrite;
                fileOffset += bytesToWrite;
            }

            if (option.HasFlushFlag())
            {
                res = Flush();
                if (res.IsFailure()) return res.Miss();
            }

            return Result.Success;
        }

        protected override Result DoFlush()
        {
            if (!_mode.HasFlag(OpenMode.Write))
                return Result.Success;

            for (int index = 0; index < _fileArray.Count; index++)
            {
                Assert.SdkNotNull(_fileArray[index]);

                Result res = _fileArray[index].Flush();
                if (res.IsFailure()) return res.Miss();
            }

            return Result.Success;
        }

        protected override Result DoSetSize(long size)
        {
            Result res = DrySetSize(size, _mode);
            if (res.IsFailure()) return res.Miss();

            res = GetSize(out long currentSize);
            if (res.IsFailure()) return res.Miss();

            if (currentSize == size) return Result.Success;

            int currentTailIndex = GetInternalFileCount(currentSize) - 1;
            int newTailIndex = GetInternalFileCount(size) - 1;

            using var internalFilePath = new Path();
            res = internalFilePath.Initialize(in _path);
            if (res.IsFailure()) return res.Miss();

            if (size > currentSize)
            {
                Assert.SdkAssert(_fileArray.Count > currentTailIndex);

                res = _fileArray[currentTailIndex].SetSize(GetInternalFileSize(size, currentTailIndex));
                if (res.IsFailure()) return res.Miss();

                for (int i = currentTailIndex + 1; i <= newTailIndex; i++)
                {
                    res = AppendInternalFilePath(ref internalFilePath.Ref(), i);
                    if (res.IsFailure()) return res.Miss();

                    res = _baseFileSystem.CreateFile(in internalFilePath, GetInternalFileSize(size, i),
                        CreateFileOptions.None);
                    if (res.IsFailure()) return res.Miss();

                    using var newInternalFile = new UniqueRef<IFile>();
                    res = _baseFileSystem.OpenFile(ref newInternalFile.Ref(), in internalFilePath, _mode);
                    if (res.IsFailure()) return res.Miss();

                    _fileArray.Add(newInternalFile.Release());

                    res = internalFilePath.RemoveChild();
                    if (res.IsFailure()) return res.Miss();
                }
            }
            else
            {
                for (int i = currentTailIndex; i > newTailIndex; i--)
                {
                    _fileArray[i].Dispose();
                    _fileArray.RemoveAt(i);

                    res = AppendInternalFilePath(ref internalFilePath.Ref(), i);
                    if (res.IsFailure()) return res.Miss();

                    res = _baseFileSystem.DeleteFile(in internalFilePath);
                    if (res.IsFailure()) return res.Miss();

                    res = internalFilePath.RemoveChild();
                    if (res.IsFailure()) return res.Miss();
                }

                res = _fileArray[newTailIndex].SetSize(GetInternalFileSize(size, newTailIndex));
                if (res.IsFailure()) return res.Miss();
            }

            return Result.Success;
        }

        protected override Result DoGetSize(out long size)
        {
            UnsafeHelpers.SkipParamInit(out size);

            long totalSize = 0;

            foreach (IFile file in _fileArray)
            {
                Result res = file.GetSize(out long internalFileSize);
                if (res.IsFailure()) return res.Miss();

                totalSize += internalFileSize;
            }

            size = totalSize;
            return Result.Success;
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            switch (operationId)
            {
                case OperationId.InvalidateCache:
                {
                    if (!_mode.HasFlag(OpenMode.Read))
                        return ResultFs.ReadUnpermitted.Log();

                    foreach (IFile file in _fileArray)
                    {
                        Result res = file.OperateRange(operationId, 0, long.MaxValue);
                        if (res.IsFailure()) return res.Miss();
                    }

                    return Result.Success;
                }
                case OperationId.QueryRange:
                {
                    if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                        return ResultFs.InvalidSize.Log();

                    var closure = new OperateRangeClosure();
                    closure.InBuffer = inBuffer;
                    closure.OperationId = operationId;
                    closure.InfoMerged.Clear();

                    Result res = DoOperateRangeImpl(offset, size, QueryRangeImpl, ref closure);
                    if (res.IsFailure()) return res.Miss();

                    SpanHelpers.AsByteSpan(ref closure.InfoMerged).CopyTo(outBuffer);
                    return Result.Success;
                }
                default:
                    return ResultFs.UnsupportedOperateRangeForConcatenationFile.Log();
            }

            static Result QueryRangeImpl(IFile file, long offset, long size, ref OperateRangeClosure closure)
            {
                Unsafe.SkipInit(out QueryRangeInfo infoEntry);

                Result res = file.OperateRange(SpanHelpers.AsByteSpan(ref infoEntry), closure.OperationId, offset, size,
                    closure.InBuffer);
                if (res.IsFailure()) return res.Miss();

                closure.InfoMerged.Merge(in infoEntry);
                return Result.Success;
            }
        }

        private Result DoOperateRangeImpl(long offset, long size, OperateRangeTask func,
            ref OperateRangeClosure closure)
        {
            if (offset < 0)
                return ResultFs.OutOfRange.Log();

            Result res = GetSize(out long currentSize);
            if (res.IsFailure()) return res.Miss();

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

                Assert.SdkAssert(fileIndex < _fileArray.Count);

                res = func(_fileArray[fileIndex], internalFileOffset, sizeToOperate, ref closure);
                if (res.IsFailure()) return res.Miss();

                remaining -= sizeToOperate;
                currentOffset += sizeToOperate;
            }

            return Result.Success;
        }

        private delegate Result OperateRangeTask(IFile file, long offset, long size, ref OperateRangeClosure closure);

        private ref struct OperateRangeClosure
        {
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
            _path = new Path.Stored();
            _baseFileSystem = baseFileSystem;
            _concatenationFileSystem = concatFileSystem;
        }

        public override void Dispose()
        {
            _path.Dispose();
            _baseDirectory.Destroy();

            base.Dispose();
        }

        public Result Initialize(in Path path)
        {
            return _path.Initialize(in path).Ret();
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            UnsafeHelpers.SkipParamInit(out entriesRead);

            Unsafe.SkipInit(out DirectoryEntry entry);
            int readCountTotal = 0;

            while (readCountTotal < entryBuffer.Length)
            {
                Result res = _baseDirectory.Get.Read(out long readCount, SpanHelpers.AsSpan(ref entry));
                if (res.IsFailure()) return res.Miss();

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
                        res = internalFilePath.Initialize(in _path);
                        if (res.IsFailure()) return res.Miss();

                        res = internalFilePath.AppendChild(entry.Name);
                        if (res.IsFailure()) return res.Miss();

                        res = _concatenationFileSystem.GetFileSize(out entry.Size, in internalFilePath);
                        if (res.IsFailure()) return res.Miss();
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

            using Path path = _path.DangerousGetPath();

            Result res = _baseFileSystem.OpenDirectory(ref directory.Ref(), in path,
                OpenDirectoryMode.All | OpenDirectoryMode.NoFileSize);
            if (res.IsFailure()) return res.Miss();

            long entryCountTotal = 0;

            while (true)
            {
                directory.Get.Read(out long readCount, SpanHelpers.AsSpan(ref entry));
                if (res.IsFailure()) return res.Miss();

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

    private UniqueRef<IAttributeFileSystem> _baseFileSystem;
    private long _internalFileSize;
    private SdkMutexType _mutex;

    /// <summary>
    /// Initializes a new <see cref="ConcatenationFileSystem"/> with an internal file size of <see cref="DefaultInternalFileSize"/>.
    /// </summary>
    /// <param name="baseFileSystem">The base <see cref="IAttributeFileSystem"/> for the
    /// new <see cref="ConcatenationFileSystem"/>.</param>
    public ConcatenationFileSystem(ref UniqueRef<IAttributeFileSystem> baseFileSystem) : this(ref baseFileSystem,
        DefaultInternalFileSize)
    { }

    /// <summary>
    /// Initializes a new <see cref="ConcatenationFileSystem"/>.
    /// </summary>
    /// <param name="baseFileSystem">The base <see cref="IAttributeFileSystem"/> for the
    /// new <see cref="ConcatenationFileSystem"/>.</param>
    /// <param name="internalFileSize">The size of each internal file. Once a file exceeds this size, a new internal file will be created</param>
    public ConcatenationFileSystem(ref UniqueRef<IAttributeFileSystem> baseFileSystem, long internalFileSize)
    {
        _baseFileSystem = new UniqueRef<IAttributeFileSystem>(ref baseFileSystem);
        _internalFileSize = internalFileSize;
        _mutex = new SdkMutexType();
    }

    public override void Dispose()
    {
        _baseFileSystem.Destroy();

        base.Dispose();
    }

    private static ReadOnlySpan<byte> RootPath => new[] { (byte)'/' };

    /// <summary>
    /// Appends the two-digit-padded <paramref name="index"/> to the given <see cref="Path"/>.
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to be modified.</param>
    /// <param name="index">The index to append to the <see cref="Path"/>.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    private static Result AppendInternalFilePath(ref Path path, int index)
    {
        var buffer = new Array3<byte>();
        Utf8Formatter.TryFormat(index, buffer.Items, out _, new StandardFormat('d', 2));

        return path.AppendChild(buffer.ItemsRo).Ret();
    }

    private static Result GenerateInternalFilePath(ref Path outPath, int index, in Path basePath)
    {
        Result res = outPath.Initialize(in basePath);
        if (res.IsFailure()) return res.Miss();

        return AppendInternalFilePath(ref outPath, index).Ret();
    }

    private static Result GenerateParentPath(ref Path outParentPath, in Path path)
    {
        if (path == RootPath)
            return ResultFs.PathNotFound.Log();

        Result res = outParentPath.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        return outParentPath.RemoveChild().Ret();
    }

    private static bool IsConcatenationFileAttribute(NxFileAttributes attribute)
    {
        return attribute.HasFlag(NxFileAttributes.Directory | NxFileAttributes.Archive);
    }

    private bool IsConcatenationFile(in Path path)
    {
        Result res = _baseFileSystem.Get.GetFileAttributes(out NxFileAttributes attribute, in path);
        if (res.IsFailure())
            return false;

        return IsConcatenationFileAttribute(attribute);
    }

    private Result GetInternalFileCount(out int count, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out count);

        using var internalFilePath = new Path();
        Result res = internalFilePath.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        for (int i = 0; ; i++)
        {
            res = AppendInternalFilePath(ref internalFilePath.Ref(), i);
            if (res.IsFailure()) return res.Miss();

            res = _baseFileSystem.Get.GetEntryType(out _, in internalFilePath);
            if (res.IsFailure())
            {
                // We've passed the last internal file of the concatenation file
                // once the next internal file doesn't exist.
                if (ResultFs.PathNotFound.Includes(res))
                {
                    res.Catch();
                    count = i;
                    res.Handle();

                    return Result.Success;
                }

                return res.Miss();
            }

            res = internalFilePath.RemoveChild();
            if (res.IsFailure()) return res.Miss();
        }
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        if (IsConcatenationFile(in path))
        {
            entryType = DirectoryEntryType.File;
            return Result.Success;
        }

        return _baseFileSystem.Get.GetEntryType(out entryType, path).Ret();
    }

    protected override Result DoGetFreeSpaceSize(out long freeSpace, in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        return _baseFileSystem.Get.GetFreeSpaceSize(out freeSpace, path).Ret();
    }

    protected override Result DoGetTotalSpaceSize(out long totalSpace, in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        return _baseFileSystem.Get.GetTotalSpaceSize(out totalSpace, path).Ret();
    }

    protected override Result DoGetFileTimeStampRaw(out FileTimeStampRaw timeStamp, in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        return _baseFileSystem.Get.GetFileTimeStampRaw(out timeStamp, path).Ret();
    }

    protected override Result DoFlush()
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        return _baseFileSystem.Get.Flush().Ret();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        if (!IsConcatenationFile(in path))
        {
            return _baseFileSystem.Get.OpenFile(ref outFile, in path, mode).Ret();
        }

        Result res = GetInternalFileCount(out int fileCount, in path);
        if (res.IsFailure()) return res.Miss();

        if (fileCount <= 0)
            return ResultFs.ConcatenationFsInvalidInternalFileCount.Log();

        var internalFiles = new List<IFile>(fileCount);

        using var filePath = new Path();
        filePath.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        try
        {
            for (int i = 0; i < fileCount; i++)
            {
                res = AppendInternalFilePath(ref filePath.Ref(), i);
                if (res.IsFailure()) return res.Miss();

                using var internalFile = new UniqueRef<IFile>();
                res = _baseFileSystem.Get.OpenFile(ref internalFile.Ref(), in filePath, mode);
                if (res.IsFailure()) return res.Miss();

                internalFiles.Add(internalFile.Release());

                res = filePath.RemoveChild();
                if (res.IsFailure()) return res.Miss();
            }

            using var concatFile = new UniqueRef<ConcatenationFile>(
                new ConcatenationFile(mode, ref internalFiles, _internalFileSize, _baseFileSystem.Get));

            res = concatFile.Get.Initialize(in path);
            if (res.IsFailure()) return res.Miss();

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
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        if (IsConcatenationFile(path))
        {
            return ResultFs.PathNotFound.Log();
        }

        using var baseDirectory = new UniqueRef<IDirectory>();
        Result res = _baseFileSystem.Get.OpenDirectory(ref baseDirectory.Ref(), path, OpenDirectoryMode.All);
        if (res.IsFailure()) return res.Miss();

        using var concatDirectory = new UniqueRef<ConcatenationDirectory>(
            new ConcatenationDirectory(mode, ref baseDirectory.Ref(), this, _baseFileSystem.Get));
        res = concatDirectory.Get.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        outDirectory.Set(ref concatDirectory.Ref());
        return Result.Success;
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        CreateFileOptions newOption = option & ~CreateFileOptions.CreateConcatenationFile;

        // Create a normal file if the concatenation file flag isn't set
        if (!option.HasFlag(CreateFileOptions.CreateConcatenationFile))
        {
            return _baseFileSystem.Get.CreateFile(path, size, newOption).Ret();
        }

        using var parentPath = new Path();
        Result res = GenerateParentPath(ref parentPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        if (IsConcatenationFile(in parentPath))
        {
            // Cannot create a file inside of a concatenation file
            return ResultFs.PathNotFound.Log();
        }

        res = _baseFileSystem.Get.CreateDirectory(in path, NxFileAttributes.Archive);
        if (res.IsFailure()) return res.Miss();

        // Handle the empty file case by manually creating a single empty internal file
        if (size == 0)
        {
            using var emptyFilePath = new Path();
            res = GenerateInternalFilePath(ref emptyFilePath.Ref(), 0, in path);
            if (res.IsFailure()) return res.Miss();

            res = _baseFileSystem.Get.CreateFile(in emptyFilePath, 0, newOption);
            if (res.IsFailure()) return res.Miss();

            return Result.Success;
        }

        long remaining = size;
        using var filePath = new Path();
        filePath.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        for (int i = 0; remaining > 0; i++)
        {
            res = AppendInternalFilePath(ref filePath.Ref(), i);
            if (res.IsFailure()) return res.Miss();

            long fileSize = Math.Min(remaining, _internalFileSize);
            Result createInternalFileResult = _baseFileSystem.Get.CreateFile(in filePath, fileSize, newOption);

            // If something goes wrong when creating an internal file, delete all the
            // internal files we've created so far and delete the directory.
            // This will allow results like insufficient space results to be returned properly.
            if (createInternalFileResult.IsFailure())
            {
                createInternalFileResult.Catch();

                for (int index = i - 1; index >= 0; index--)
                {
                    res = GenerateInternalFilePath(ref filePath.Ref(), index, in path);
                    if (res.IsFailure())
                    {
                        createInternalFileResult.Handle();
                        return res.Miss();
                    }

                    if (_baseFileSystem.Get.DeleteFile(in filePath).IsFailure())
                        break;
                }

                _baseFileSystem.Get.DeleteDirectoryRecursively(in path).IgnoreResult();
                return createInternalFileResult.Rethrow();
            }

            res = filePath.RemoveChild();
            if (res.IsFailure()) return res.Miss();

            remaining -= fileSize;
        }

        return Result.Success;
    }

    protected override Result DoDeleteFile(in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        if (!IsConcatenationFile(in path))
        {
            return _baseFileSystem.Get.DeleteFile(in path).Ret();
        }

        Result res = GetInternalFileCount(out int count, path);
        if (res.IsFailure()) return res.Miss();

        using var filePath = new Path();
        res = filePath.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        for (int i = count - 1; i >= 0; i--)
        {
            res = AppendInternalFilePath(ref filePath.Ref(), i);
            if (res.IsFailure()) return res.Miss();

            res = _baseFileSystem.Get.DeleteFile(in filePath);
            if (res.IsFailure()) return res.Miss();

            res = filePath.RemoveChild();
            if (res.IsFailure()) return res.Miss();
        }

        res = _baseFileSystem.Get.DeleteDirectoryRecursively(in path);
        if (res.IsFailure()) return res.Miss();

        return Result.Success;
    }

    protected override Result DoCreateDirectory(in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        // Check if the parent path is a concatenation file because we can't create a directory inside one.
        using var parentPath = new Path();
        Result res = GenerateParentPath(ref parentPath.Ref(), in path);
        if (res.IsFailure()) return res.Miss();

        if (IsConcatenationFile(in parentPath))
            return ResultFs.PathNotFound.Log();

        return _baseFileSystem.Get.CreateDirectory(in path).Ret();
    }

    protected override Result DoDeleteDirectory(in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        // Make sure the directory isn't a concatenation file.
        if (IsConcatenationFile(path))
            return ResultFs.PathNotFound.Log();

        return _baseFileSystem.Get.DeleteDirectory(path).Ret();
    }

    private Result CleanDirectoryRecursivelyImpl(in Path path)
    {
        static Result OnEnterDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
            Result.Success;

        static Result OnExitDir(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
            closure.SourceFileSystem.DeleteDirectory(in path).Ret();

        static Result OnFile(in Path path, in DirectoryEntry entry, ref FsIterationTaskClosure closure) =>
            closure.SourceFileSystem.DeleteFile(in path).Ret();

        var closure = new FsIterationTaskClosure();
        closure.SourceFileSystem = this;

        var directoryEntry = new DirectoryEntry();
        return CleanupDirectoryRecursively(this, in path, ref directoryEntry, OnEnterDir, OnExitDir, OnFile,
            ref closure).Ret();
    }

    protected override Result DoDeleteDirectoryRecursively(in Path path)
    {
        bool isConcatenationFile;
        using (new ScopedLock<SdkMutexType>(ref _mutex))
        {
            isConcatenationFile = IsConcatenationFile(in path);
        }

        if (isConcatenationFile)
            return ResultFs.PathNotFound.Log();

        Result res = CleanDirectoryRecursivelyImpl(in path);
        if (res.IsFailure()) return res.Miss();

        return _baseFileSystem.Get.DeleteDirectory(in path).Ret();
    }

    protected override Result DoCleanDirectoryRecursively(in Path path)
    {
        bool isConcatenationFile;
        using (new ScopedLock<SdkMutexType>(ref _mutex))
        {
            isConcatenationFile = IsConcatenationFile(in path);
        }

        if (isConcatenationFile)
            return ResultFs.PathNotFound.Log();

        return CleanDirectoryRecursivelyImpl(in path).Ret();
    }

    protected override Result DoRenameFile(in Path currentPath, in Path newPath)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        if (IsConcatenationFile(in currentPath))
            return _baseFileSystem.Get.RenameDirectory(in currentPath, in newPath).Ret();

        return _baseFileSystem.Get.RenameFile(in currentPath, in newPath).Ret();
    }

    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        if (IsConcatenationFile(in currentPath))
            return ResultFs.PathNotFound.Log();

        return _baseFileSystem.Get.RenameDirectory(in currentPath, in newPath).Ret();
    }

    public Result GetFileSize(out long size, in Path path)
    {
        UnsafeHelpers.SkipParamInit(out size);

        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        using var internalFilePath = new Path();
        Result res = internalFilePath.Initialize(in path);
        if (res.IsFailure()) return res.Miss();

        long sizeTotal = 0;

        for (int i = 0; ; i++)
        {
            res = AppendInternalFilePath(ref internalFilePath.Ref(), i);
            if (res.IsFailure()) return res.Miss();

            res = _baseFileSystem.Get.GetFileSize(out long internalFileSize, in internalFilePath);
            if (res.IsFailure())
            {
                // We've passed the last internal file of the concatenation file
                // once the next internal file doesn't exist.
                if (ResultFs.PathNotFound.Includes(res))
                {
                    res.Catch();
                    size = sizeTotal;
                    res.Handle();

                    return Result.Success;
                }

                return res.Miss();
            }

            res = internalFilePath.RemoveChild();
            if (res.IsFailure()) return res.Miss();

            sizeTotal += internalFileSize;
        }
    }

    protected override Result DoQueryEntry(Span<byte> outBuffer, ReadOnlySpan<byte> inBuffer, QueryId queryId,
        in Path path)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        if (queryId != QueryId.SetConcatenationFileAttribute)
            return ResultFs.UnsupportedQueryEntryForConcatenationFileSystem.Log();

        return _baseFileSystem.Get.SetFileAttributes(in path, NxFileAttributes.Archive).Ret();
    }

    protected override Result DoCommit()
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        return _baseFileSystem.Get.Commit().Ret();
    }

    protected override Result DoCommitProvisionally(long counter)
    {
        using var scopedLock = new ScopedLock<SdkMutexType>(ref _mutex);

        return _baseFileSystem.Get.CommitProvisionally(counter).Ret();
    }
}