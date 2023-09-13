using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Crypto;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Util;
using Buffer = LibHac.Mem.Buffer;

namespace LibHac.FsSystem;

/// <summary>
/// The allocator used by a <see cref="PartitionFileSystemCore{TMetaData,TFormat,THeader,TEntry}"/> when none is provided.
/// </summary>
/// <remarks><para>The original allocator in FS simply calls <c>nn::fs::detail::Allocate</c> and
/// <c>nn::fs::detail::Deallocate</c>. In our implementation we use the shared .NET <see cref="ArrayPool{T}"/>.</para>
/// <para>Based on nnSdk 16.2.0 (FS 16.0.0)</para></remarks>
file sealed class DefaultAllocatorForPartitionFileSystem : MemoryResource
{
    public static readonly DefaultAllocatorForPartitionFileSystem Instance = new();

    protected override Buffer DoAllocate(long size, int alignment)
    {
        byte[] array = ArrayPool<byte>.Shared.Rent((int)size);

        return new Buffer(array.AsMemory(0, (int)size), array);
    }

    protected override void DoDeallocate(Buffer buffer, int alignment)
    {
        if (buffer.Extra is byte[] array)
        {
            ArrayPool<byte>.Shared.Return(array);
        }
        else
        {
            throw new LibHacException("Buffer was not allocated by this MemoryResource.");
        }
    }

    protected override bool DoIsEqual(MemoryResource other)
    {
        return ReferenceEquals(this, other);
    }
}

/// <summary>
/// Reads a standard partition file system. These files start with "PFS0" and are typically found inside NCAs
/// or as .nsp files.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class PartitionFileSystem : PartitionFileSystemCore<PartitionFileSystemMeta,
    Impl.PartitionFileSystemFormat,
    Impl.PartitionFileSystemFormat.PartitionFileSystemHeaderImpl,
    Impl.PartitionFileSystemFormat.PartitionEntry> { }

/// <summary>
/// Reads a hashed partition file system. These files start with "HFS0" and are typically found inside XCIs.
/// </summary>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class Sha256PartitionFileSystem : PartitionFileSystemCore<Sha256PartitionFileSystemMeta,
    Impl.Sha256PartitionFileSystemFormat,
    Impl.PartitionFileSystemFormat.PartitionFileSystemHeaderImpl,
    Impl.Sha256PartitionFileSystemFormat.PartitionEntry> { }

/// <summary>
/// Provides the base for an <see cref="IFileSystem"/> that can read from different partition file system files.
/// A partition file system is a simple, flat file archive that can't contain any directories. The archive has
/// two main sections: the metadata located at the start of the file, and the actual file data located directly after.
/// </summary>
/// <typeparam name="TMetaData">The type of the class used to read this file system's metadata.</typeparam>
/// <typeparam name="TFormat">A traits class that provides values used to read and build the metadata.</typeparam>
/// <typeparam name="THeader">The type of the header at the beginning of the metadata.</typeparam>
/// <typeparam name="TEntry">The type of the entries in the file table in the metadata.</typeparam>
/// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
public class PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> : IFileSystem
    where TMetaData : PartitionFileSystemMetaCore<TFormat, THeader, TEntry>, new()
    where TFormat : IPartitionFileSystemFormat
    where THeader : unmanaged, IPartitionFileSystemHeader
    where TEntry : unmanaged, IPartitionFileSystemEntry
{
    private static ReadOnlySpan<byte> RootPath => "/"u8;

    private IStorage _baseStorage;
    private TMetaData _metaData;
    private bool _isInitialized;
    private long _metaDataSize;
    private UniqueRef<TMetaData> _uniqueMetaData;
    private SharedRef<IStorage> _sharedStorage;

    /// <summary>
    /// Provides access to a file from a <see cref="PartitionFileSystemCore{TMetaData,TFormat,THeader,TEntry}"/>.
    /// </summary>
    /// <remarks>Based on nnSdk 16.2.0 (FS 16.0.0)</remarks>
    private class PartitionFile : IFile
    {
        private TEntry _partitionEntry;
        private readonly PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> _parent;
        private readonly OpenMode _mode;

        public PartitionFile(PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> parent, in TEntry partitionEntry, OpenMode mode)
        {
            _partitionEntry = partitionEntry;
            _parent = parent;
            _mode = mode;
        }

        protected override Result DoWrite(long offset, ReadOnlySpan<byte> source, in WriteOption option)
        {
            Result res = DryWrite(out bool needsAppend, offset, source.Length, in option, _mode);
            if (res.IsFailure()) return res.Miss();

            if (needsAppend)
                return ResultFs.UnsupportedWriteForPartitionFile.Log();

            Assert.SdkRequires(!_mode.HasFlag(OpenMode.AllowAppend));

            if (offset > _partitionEntry.Size)
                return ResultFs.OutOfRange.Log();

            if (offset + source.Length > _partitionEntry.Size)
                return ResultFs.InvalidSize.Log();

            return _parent._baseStorage.Write(_parent._metaDataSize + _partitionEntry.Offset + offset, source).Ret();
        }

        protected override Result DoFlush()
        {
            if (!_mode.HasFlag(OpenMode.Write))
                return Result.Success;

            return _parent._baseStorage.Flush().Ret();
        }

        protected override Result DoSetSize(long size)
        {
            Result res = DrySetSize(size, _mode);
            if (res.IsFailure()) return res.Miss();

            return ResultFs.UnsupportedWriteForPartitionFile.Log();
        }

        protected override Result DoGetSize(out long size)
        {
            size = _partitionEntry.Size;
            return Result.Success;
        }

        protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size,
            ReadOnlySpan<byte> inBuffer)
        {
            long operateOffset;
            long operateSize;

            switch (operationId)
            {
                case OperationId.InvalidateCache:
                    if (!_mode.HasFlag(OpenMode.Read))
                        return ResultFs.ReadUnpermitted.Log();

                    if (_mode.HasFlag(OpenMode.Write))
                        return ResultFs.UnsupportedOperateRangeForPartitionFile.Log();

                    operateOffset = 0;
                    operateSize = long.MaxValue;
                    break;

                case OperationId.QueryRange:
                    if (offset < 0 || offset > _partitionEntry.Size)
                        return ResultFs.OutOfRange.Log();

                    if (offset + size > _partitionEntry.Size || offset + size < offset)
                        return ResultFs.InvalidSize.Log();

                    operateOffset = _parent._metaDataSize + _partitionEntry.Offset + offset;
                    operateSize = size;
                    break;

                default:
                    return ResultFs.UnsupportedOperateRangeForPartitionFile.Log();
            }

            return _parent._baseStorage.OperateRange(outBuffer, operationId, operateOffset, operateSize, inBuffer).Ret();
        }

        protected override Result DoRead(out long bytesRead, long offset, Span<byte> destination, in ReadOption option)
        {
            if (this is PartitionFileSystem.PartitionFile file)
            {
                return DoRead(file, out bytesRead, offset, destination, in option).Ret();
            }

            if (this is Sha256PartitionFileSystem.PartitionFile fileSha)
            {
                return DoRead(fileSha, out bytesRead, offset, destination, in option).Ret();
            }

            UnsafeHelpers.SkipParamInit(out bytesRead);
            Abort.DoAbort("PartitionFileSystemCore.PartitionFile type is not supported.");
            return ResultFs.NotImplemented.Log();
        }

        private static Result DoRead(Sha256PartitionFileSystem.PartitionFile fs, out long bytesRead, long offset,
            Span<byte> destination, in ReadOption option)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            Result res = fs.DryRead(out long readSize, offset, destination.Length, in option, fs._mode);
            if (res.IsFailure()) return res.Miss();

            long entryStart = fs._parent._metaDataSize + fs._partitionEntry.Offset;
            long readEnd = offset + readSize;
            long hashTargetStart = fs._partitionEntry.HashTargetOffset;
            long hashTargetEnd = hashTargetStart + fs._partitionEntry.HashTargetSize;

            if (readEnd > hashTargetStart && hashTargetEnd > offset)
            {
                // The portion we're reading contains at least some of the hashed region.

                // Only hash target offset == 0 is supported.
                if (hashTargetStart != 0)
                    return ResultFs.InvalidSha256PartitionHashTarget.Log();

                // Ensure that the hashed region doesn't extend past the end of the file.
                if (hashTargetEnd > fs._partitionEntry.Size)
                    return ResultFs.InvalidSha256PartitionHashTarget.Log();

                // Validate our read offset.
                long readOffset = entryStart + offset;
                if (readOffset < offset)
                    return ResultFs.OutOfRange.Log();

                // Prepare a buffer for our calculated hash.
                Span<byte> hash = stackalloc byte[Sha256Generator.HashSize];
                var sha = new Sha256Generator();

                if (offset <= hashTargetStart && hashTargetEnd <= readEnd)
                {
                    // Easy case: the portion we're reading contains the entire hashed region.
                    sha.Initialize();

                    res = fs._parent._baseStorage.Read(readOffset, destination.Slice(0, (int)readSize));
                    if (res.IsFailure()) return res.Miss();

                    sha.Update(destination.Slice((int)(hashTargetStart - offset), fs._partitionEntry.HashTargetSize));
                    sha.GetHash(hash);
                }
                else if (hashTargetStart <= offset && readEnd <= hashTargetEnd)
                {
                    // The portion we're reading is located entirely within the hashed region.
                    int remainingHashTargetSize = fs._partitionEntry.HashTargetSize;
                    // ReSharper disable once UselessBinaryOperation
                    // We still want to allow the code to handle any hash target start offset even though it's currently restricted to being only 0.
                    long currentHashTargetOffset = entryStart + hashTargetStart;
                    long remainingSize = readSize;
                    int destBufferOffset = 0;

                    sha.Initialize();

                    const int bufferForHashTargetSize = 0x200;
                    Span<byte> bufferForHashTarget = stackalloc byte[bufferForHashTargetSize];

                    // Loop over the entire hashed region to calculate the hash.
                    while (remainingHashTargetSize > 0)
                    {
                        // Read the next chunk of the hash target and update the hash.
                        int currentReadSize = Math.Min(bufferForHashTargetSize, remainingHashTargetSize);
                        Span<byte> currentHashTargetBuffer = bufferForHashTarget.Slice(0, currentReadSize);

                        res = fs._parent._baseStorage.Read(currentHashTargetOffset, currentHashTargetBuffer);
                        if (res.IsFailure()) return res.Miss();

                        sha.Update(currentHashTargetBuffer);

                        // Check if the chunk we just read contains any of the requested range.
                        if (readOffset <= currentHashTargetOffset + currentReadSize && remainingSize > 0)
                        {
                            // Copy the relevant portion of the chunk into the destination buffer.
                            int hashTargetBufferOffset = (int)Math.Max(readOffset - currentHashTargetOffset, 0);
                            int copySize = (int)Math.Min(currentReadSize - hashTargetBufferOffset, remainingSize);

                            bufferForHashTarget.Slice(hashTargetBufferOffset, copySize).CopyTo(destination.Slice(destBufferOffset));

                            remainingSize -= copySize;
                            destBufferOffset += copySize;
                        }

                        remainingHashTargetSize -= currentReadSize;
                        currentHashTargetOffset += currentReadSize;
                    }

                    sha.GetHash(hash);
                }
                else
                {
                    return ResultFs.InvalidSha256PartitionHashTarget.Log();
                }

                if (!CryptoUtil.IsSameBytes(fs._partitionEntry.Hash, hash, hash.Length))
                {
                    destination.Slice(0, (int)readSize).Clear();
                    return ResultFs.Sha256PartitionHashVerificationFailed.Log();
                }
            }
            else
            {
                // We aren't reading hashed data, so we can just read from the base storage.
                res = fs._parent._baseStorage.Read(entryStart + offset, destination.Slice(0, (int)readSize));
                if (res.IsFailure()) return res.Miss();
            }

            bytesRead = readSize;
            return Result.Success;
        }

        private static Result DoRead(PartitionFileSystem.PartitionFile fs, out long bytesRead, long offset,
            Span<byte> destination, in ReadOption option)
        {
            UnsafeHelpers.SkipParamInit(out bytesRead);

            Result res = fs.DryRead(out long readSize, offset, destination.Length, in option, fs._mode);
            if (res.IsFailure()) return res.Miss();

            res = fs._parent._baseStorage.Read(fs._parent._metaDataSize + fs._partitionEntry.Offset + offset,
                destination.Slice(0, (int)readSize));
            if (res.IsFailure()) return res.Miss();

            bytesRead = readSize;
            return Result.Success;
        }
    }

    /// <summary>
    /// Provides access to the root directory from a <see cref="PartitionFileSystemCore{TMetaData,TFormat,THeader,TEntry}"/>.
    /// </summary>
    /// <remarks><para>A <see cref="PartitionFileSystemCore{TMetaData,TFormat,THeader,TEntry}"/> cannot contain any
    /// subdirectories, so a <see cref="PartitionDirectory"/> will only access the root directory.</para>
    /// <para>Based on nnSdk 16.2.0 (FS 16.0.0)</para></remarks>
    private class PartitionDirectory : IDirectory
    {
        private int _currentIndex;
        private readonly PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> _parent;
        private readonly OpenDirectoryMode _mode;

        public PartitionDirectory(PartitionFileSystemCore<TMetaData, TFormat, THeader, TEntry> parent, OpenDirectoryMode mode)
        {
            _currentIndex = 0;
            _parent = parent;
            _mode = mode;
        }

        protected override Result DoRead(out long entriesRead, Span<DirectoryEntry> entryBuffer)
        {
            if (!_mode.HasFlag(OpenDirectoryMode.File))
            {
                // A partition file system can't contain any subdirectories.
                entriesRead = 0;
                return Result.Success;
            }

            int entryCount = Math.Min(entryBuffer.Length, _parent._metaData.GetEntryCount() - _currentIndex);

            for (int i = 0; i < entryCount; i++)
            {
                ref readonly TEntry entry = ref _parent._metaData.GetEntry(_currentIndex);
                ref DirectoryEntry dirEntry = ref entryBuffer[i];

                dirEntry.Type = DirectoryEntryType.File;
                dirEntry.Size = entry.Size;
                U8Span entryName = _parent._metaData.GetEntryName(_currentIndex);
                StringUtils.Strlcpy(dirEntry.Name.Items, entryName, dirEntry.Name.ItemsRo.Length - 1);

                _currentIndex++;
            }

            entriesRead = entryCount;
            return Result.Success;
        }

        protected override Result DoGetEntryCount(out long entryCount)
        {
            if (_mode.HasFlag(OpenDirectoryMode.File))
            {
                entryCount = _parent._metaData.GetEntryCount();
            }
            else
            {
                entryCount = 0;
            }

            return Result.Success;
        }
    }

    public PartitionFileSystemCore()
    {
        _isInitialized = false;
    }

    public override void Dispose()
    {
        _sharedStorage.Destroy();
        _uniqueMetaData.Destroy();
        base.Dispose();
    }

    public Result Initialize(in SharedRef<IStorage> baseStorage)
    {
        _sharedStorage.SetByCopy(in baseStorage);

        return Initialize(_sharedStorage.Get).Ret();
    }

    public Result Initialize(in SharedRef<IStorage> baseStorage, MemoryResource allocator)
    {
        _sharedStorage.SetByCopy(in baseStorage);

        return Initialize(_sharedStorage.Get, allocator).Ret();
    }

    public Result Initialize(IStorage baseStorage)
    {
        return Initialize(baseStorage, DefaultAllocatorForPartitionFileSystem.Instance).Ret();
    }

    private Result Initialize(IStorage baseStorage, MemoryResource allocator)
    {
        if (_isInitialized)
            return ResultFs.PreconditionViolation.Log();

        _uniqueMetaData.Reset(new TMetaData());
        if (!_uniqueMetaData.HasValue)
            return ResultFs.AllocationMemoryFailedInPartitionFileSystemA.Log();

        Result res = _uniqueMetaData.Get.Initialize(baseStorage, allocator);
        if (res.IsFailure()) return res.Miss();

        _metaData = _uniqueMetaData.Get;
        _baseStorage = baseStorage;
        _metaDataSize = _metaData.GetMetaDataSize();
        _isInitialized = true;

        return Result.Success;
    }

    public Result Initialize(ref UniqueRef<TMetaData> metaData, in SharedRef<IStorage> baseStorage)
    {
        _uniqueMetaData.Set(ref metaData);

        return Initialize(_uniqueMetaData.Get, in baseStorage).Ret();
    }

    public Result Initialize(TMetaData metaData, in SharedRef<IStorage> baseStorage)
    {
        if (_isInitialized)
            return ResultFs.PreconditionViolation.Log();

        _sharedStorage.SetByCopy(in baseStorage);
        _baseStorage = _sharedStorage.Get;
        _metaData = metaData;
        _metaDataSize = _metaData.GetMetaDataSize();
        _isInitialized = true;

        return Result.Success;
    }

    public Result GetFileBaseOffset(out long outOffset, U8Span path)
    {
        UnsafeHelpers.SkipParamInit(out outOffset);

        if (!_isInitialized)
            return ResultFs.PreconditionViolation.Log();

        if (path.Length == 0)
            return ResultFs.PathNotFound.Log();

        int entryIndex = _metaData.GetEntryIndex(path.Slice(1));
        if (entryIndex < 0)
            return ResultFs.PathNotFound.Log();

        outOffset = _metaDataSize + _metaData.GetEntry(entryIndex).Offset;
        return Result.Success;
    }

    protected override Result DoGetEntryType(out DirectoryEntryType entryType, in Path path)
    {
        Unsafe.SkipInit(out entryType);

        if (!_isInitialized)
            return ResultFs.PreconditionViolation.Log();

        ReadOnlySpan<byte> pathString = path.GetString();
        if (pathString.At(0) != RootPath[0])
            return ResultFs.InvalidPathFormat.Log();

        if (StringUtils.Compare(RootPath, pathString, RootPath.Length + 1) == 0)
        {
            entryType = DirectoryEntryType.Directory;
            return Result.Success;
        }

        if (_metaData.GetEntryIndex(pathString.Slice(1)) >= 0)
        {
            entryType = DirectoryEntryType.File;
            return Result.Success;
        }

        return ResultFs.PathNotFound.Log();
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        if (!_isInitialized)
            return ResultFs.PreconditionViolation.Log();

        // LibHac addition to catch empty strings
        if (path.GetString().Length == 0)
            return ResultFs.PathNotFound.Log();

        int entryIndex = _metaData.GetEntryIndex(path.GetString().Slice(1));
        if (entryIndex < 0)
            return ResultFs.PathNotFound.Log();

        using var file = new UniqueRef<PartitionFile>(new PartitionFile(this, in _metaData.GetEntry(entryIndex), mode));
        if (!file.HasValue)
            return ResultFs.AllocationMemoryFailedInPartitionFileSystemB.Log();

        outFile.Set(ref file.Ref);
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path, OpenDirectoryMode mode)
    {
        if (!_isInitialized)
            return ResultFs.PreconditionViolation.Log();

        if (!(path == RootPath))
            return ResultFs.PathNotFound.Log();

        using var directory = new UniqueRef<PartitionDirectory>(new PartitionDirectory(this, mode));
        if (!directory.HasValue)
            return ResultFs.AllocationMemoryFailedInPartitionFileSystemC.Log();

        outDirectory.Set(ref directory.Ref);
        return Result.Success;
    }

    protected override Result DoCreateFile(in Path path, long size, CreateFileOptions option) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
    protected override Result DoDeleteFile(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
    protected override Result DoCreateDirectory(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
    protected override Result DoDeleteDirectory(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
    protected override Result DoDeleteDirectoryRecursively(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
    protected override Result DoCleanDirectoryRecursively(in Path path) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
    protected override Result DoRenameFile(in Path currentPath, in Path newPath) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();
    protected override Result DoRenameDirectory(in Path currentPath, in Path newPath) => ResultFs.UnsupportedWriteForPartitionFileSystem.Log();

    protected override Result DoCommit()
    {
        return Result.Success;
    }

    protected override Result DoCommitProvisionally(long counter) => ResultFs.UnsupportedCommitProvisionallyForPartitionFileSystem.Log();
}