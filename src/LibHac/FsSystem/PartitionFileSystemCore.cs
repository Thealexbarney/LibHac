using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem.Impl;
using LibHac.Util;

namespace LibHac.FsSystem
{
    public class PartitionFileSystemCore<T> : IFileSystem where T : unmanaged, IPartitionFileSystemEntry
    {
        private IStorage BaseStorage { get; set; }
        private PartitionFileSystemMetaCore<T> MetaData { get; set; }
        private bool IsInitialized { get; set; }
        private int DataOffset { get; set; }
        private ReferenceCountedDisposable<IStorage> BaseStorageShared { get; set; }

        public Result Initialize(ReferenceCountedDisposable<IStorage> baseStorage)
        {
            Result rc = Initialize(baseStorage.Target);
            if (rc.IsFailure()) return rc;

            BaseStorageShared = baseStorage.AddReference();
            return Result.Success;
        }

        public Result Initialize(IStorage baseStorage)
        {
            if (IsInitialized)
                return ResultFs.PreconditionViolation.Log();

            MetaData = new PartitionFileSystemMetaCore<T>();

            Result rc = MetaData.Initialize(baseStorage);
            if (rc.IsFailure()) return rc;

            BaseStorage = baseStorage;
            DataOffset = MetaData.Size;
            IsInitialized = true;

            return Result.Success;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                BaseStorageShared?.Dispose();
            }

            base.Dispose(disposing);
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            if (!IsInitialized)
                return ResultFs.PreconditionViolation.Log();

            ReadOnlySpan<byte> rootPath = new[] { (byte)'/' };

            if (StringUtils.Compare(rootPath, path, 2) != 0)
                return ResultFs.PathNotFound.Log();

            directory = new PartitionDirectory(this, mode);

            return Result.Success;
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            if (!IsInitialized)
                return ResultFs.PreconditionViolation.Log();

            if (!mode.HasFlag(OpenMode.Read) && !mode.HasFlag(OpenMode.Write))
                return ResultFs.InvalidArgument.Log();

            int entryIndex = MetaData.FindEntry(path.Slice(1));
            if (entryIndex < 0) return ResultFs.PathNotFound.Log();

            ref T entry = ref MetaData.GetEntry(entryIndex);

            file = new PartitionFile(this, ref entry, mode);

            return Result.Success;
        }

        protected override Result DoGetEntryType(out DirectoryEntryType entryType, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out entryType);

            if (!IsInitialized)
                return ResultFs.PreconditionViolation.Log();

            if (path.IsEmpty() || path[0] != '/')
                return ResultFs.InvalidPathFormat.Log();

            ReadOnlySpan<byte> rootPath = new[] { (byte)'/' };

            if (StringUtils.Compare(rootPath, path, 2) == 0)
            {
                entryType = DirectoryEntryType.Directory;
                return Result.Success;
            }

            if (MetaData.FindEntry(path.Slice(1)) >= 0)
            {
                entryType = DirectoryEntryType.File;
                return Result.Success;
            }

            return ResultFs.PathNotFound.Log();
        }

        protected override Result DoCommit()
        {
            return Result.Success;
        }

        protected override Result DoCreateDirectory(U8Span path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoCreateFile(U8Span path, long size, CreateFileOptions options) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoDeleteDirectory(U8Span path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoDeleteDirectoryRecursively(U8Span path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoCleanDirectoryRecursively(U8Span path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoDeleteFile(U8Span path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoRenameDirectory(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoRenameFile(U8Span oldPath, U8Span newPath) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
        protected override Result DoCommitProvisionally(long counter) => ResultFs.UnsupportedCommitProvisionallyForPartitionFileSystem.Log();

        private class PartitionFile : IFile
        {
            private PartitionFileSystemCore<T> ParentFs { get; }
            private OpenMode Mode { get; }
            private T _entry;

            public PartitionFile(PartitionFileSystemCore<T> parentFs, ref T entry, OpenMode mode)
            {
                ParentFs = parentFs;
                _entry = entry;
                Mode = mode;
            }

            protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination,
                in ReadOption option)
            {
                UnsafeHelpers.SkipParamInit(out bytesRead);

                Result rc = DryRead(out long bytesToRead, offset, destination.Length, in option, Mode);
                if (rc.IsFailure()) return rc;

                bool hashNeeded = false;
                long fileStorageOffset = ParentFs.DataOffset + _entry.Offset;

                if (typeof(T) == typeof(HashedEntry))
                {
                    ref HashedEntry entry = ref Unsafe.As<T, HashedEntry>(ref _entry);

                    long readEnd = offset + destination.Length;
                    long hashEnd = entry.HashOffset + entry.HashSize;

                    // The hash must be checked if any part of the hashed region is read
                    hashNeeded = entry.HashOffset < readEnd && hashEnd >= offset;
                }

                if (!hashNeeded)
                {
                    rc = ParentFs.BaseStorage.Read(fileStorageOffset + offset, destination.Slice(0, (int)bytesToRead));
                }
                else
                {
                    ref HashedEntry entry = ref Unsafe.As<T, HashedEntry>(ref _entry);

                    long readEnd = offset + destination.Length;
                    long hashEnd = entry.HashOffset + entry.HashSize;

                    // Make sure the hashed region doesn't extend past the end of the file
                    // N's code requires that the hashed region starts at the beginning of the file
                    if (entry.HashOffset != 0 || hashEnd > entry.Size)
                        return ResultFs.InvalidSha256PartitionHashTarget.Log();

                    long storageOffset = fileStorageOffset + offset;

                    // Nintendo checks for overflow here but not in other places for some reason
                    if (storageOffset < 0)
                        return ResultFs.OutOfRange.Log();

                    IHash sha256 = Sha256.CreateSha256Generator();
                    sha256.Initialize();

                    var actualHash = new Buffer32();

                    // If the area to read contains the entire hashed area
                    if (entry.HashOffset >= offset && hashEnd <= readEnd)
                    {
                        rc = ParentFs.BaseStorage.Read(storageOffset, destination.Slice(0, (int)bytesToRead));
                        if (rc.IsFailure()) return rc;

                        Span<byte> hashedArea = destination.Slice((int)(entry.HashOffset - offset), entry.HashSize);
                        sha256.Update(hashedArea);
                    }
                    else
                    {
                        // Can't start a read in the middle of the hashed region
                        if (readEnd > hashEnd || entry.HashOffset > offset)
                        {
                            return ResultFs.InvalidSha256PartitionHashTarget.Log();
                        }

                        int hashRemaining = entry.HashSize;
                        int readRemaining = (int)bytesToRead;
                        long readPos = fileStorageOffset + entry.HashOffset;
                        int outBufPos = 0;

                        const int hashBufferSize = 0x200;
                        Span<byte> hashBuffer = stackalloc byte[hashBufferSize];

                        while (hashRemaining > 0)
                        {
                            int toRead = Math.Min(hashRemaining, hashBufferSize);
                            Span<byte> hashBufferSliced = hashBuffer.Slice(0, toRead);

                            rc = ParentFs.BaseStorage.Read(readPos, hashBufferSliced);
                            if (rc.IsFailure()) return rc;

                            sha256.Update(hashBufferSliced);

                            if (readRemaining > 0 && storageOffset <= readPos + toRead)
                            {
                                int hashBufferOffset = (int)Math.Max(storageOffset - readPos, 0);
                                int toCopy = Math.Min(readRemaining, toRead - hashBufferOffset);

                                hashBuffer.Slice(hashBufferOffset, toCopy).CopyTo(destination.Slice(outBufPos));

                                outBufPos += toCopy;
                                readRemaining -= toCopy;
                            }

                            hashRemaining -= toRead;
                            readPos += toRead;
                        }
                    }

                    sha256.GetHash(actualHash);

                    if (!CryptoUtil.IsSameBytes(entry.Hash, actualHash, Sha256.DigestSize))
                    {
                        destination.Slice(0, (int)bytesToRead).Clear();

                        return ResultFs.Sha256PartitionHashVerificationFailed.Log();
                    }

                    rc = Result.Success;
                }

                if (rc.IsSuccess())
                    bytesRead = bytesToRead;

                return rc;
            }

            protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
            {
                Result rc = DryWrite(out bool isResizeNeeded, offset, source.Length, in option, Mode);
                if (rc.IsFailure()) return rc;

                if (isResizeNeeded)
                    return ResultFs.UnsupportedWriteForPartitionFile.Log();

                if (_entry.Size < offset)
                    return ResultFs.OutOfRange.Log();

                if (_entry.Size < source.Length + offset)
                    return ResultFs.InvalidSize.Log();

                return ParentFs.BaseStorage.Write(ParentFs.DataOffset + _entry.Offset + offset, source);
            }

            protected override Result DoFlush()
            {
                if (Mode.HasFlag(OpenMode.Write))
                {
                    return ParentFs.BaseStorage.Flush();
                }

                return Result.Success;
            }

            protected override Result DoSetSize(long size)
            {
                if (Mode.HasFlag(OpenMode.Write))
                {
                    return ResultFs.UnsupportedWriteForPartitionFile.Log();
                }

                return ResultFs.WriteUnpermitted.Log();
            }

            protected override Result DoGetSize(out long size)
            {
                size = _entry.Size;

                return Result.Success;
            }

            protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
            {
                switch (operationId)
                {
                    case OperationId.InvalidateCache:
                        if (!Mode.HasFlag(OpenMode.Read))
                            return ResultFs.ReadUnpermitted.Log();

                        if (Mode.HasFlag(OpenMode.Write))
                            return ResultFs.UnsupportedOperateRangeForPartitionFile.Log();

                        break;
                    case OperationId.QueryRange:
                        break;
                    default:
                        return ResultFs.UnsupportedOperateRangeForPartitionFile.Log();
                }

                if (offset < 0 || offset > _entry.Size)
                    return ResultFs.OutOfRange.Log();

                if (size < 0 || offset + size > _entry.Size)
                    return ResultFs.InvalidSize.Log();

                long offsetInStorage = ParentFs.DataOffset + _entry.Offset + offset;

                return ParentFs.BaseStorage.OperateRange(outBuffer, operationId, offsetInStorage, size, inBuffer);
            }
        }

        private class PartitionDirectory : IDirectory
        {
            private PartitionFileSystemCore<T> ParentFs { get; }
            private int CurrentIndex { get; set; }
            private OpenDirectoryMode Mode { get; }

            public PartitionDirectory(PartitionFileSystemCore<T> parentFs, OpenDirectoryMode mode)
            {
                ParentFs = parentFs;
                CurrentIndex = 0;
                Mode = mode;
            }

            protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
            {
                if (Mode.HasFlag(OpenDirectoryMode.File))
                {
                    int totalEntryCount = ParentFs.MetaData.GetEntryCount();
                    int toReadCount = Math.Min(totalEntryCount - CurrentIndex, entryBuffer.Length);

                    for (int i = 0; i < toReadCount; i++)
                    {
                        entryBuffer[i].Type = DirectoryEntryType.File;
                        entryBuffer[i].Size = ParentFs.MetaData.GetEntry(CurrentIndex).Size;

                        U8Span name = ParentFs.MetaData.GetName(CurrentIndex);
                        StringUtils.Copy(entryBuffer[i].Name, name);
                        entryBuffer[i].Name[FsPath.MaxLength] = 0;

                        CurrentIndex++;
                    }

                    entriesRead = toReadCount;
                }
                else
                {
                    entriesRead = 0;
                }

                return Result.Success;
            }

            protected override Result DoGetEntryCount(out long entryCount)
            {
                if (Mode.HasFlag(OpenDirectoryMode.File))
                {
                    entryCount = ParentFs.MetaData.GetEntryCount();
                }
                else
                {
                    entryCount = 0;
                }

                return Result.Success;
            }
        }
    }
}
