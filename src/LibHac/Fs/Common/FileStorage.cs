using System;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs.Fsa;
using LibHac.Os;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

/// <summary>
/// Allows interacting with an <see cref="IFile"/> via an <see cref="IStorage"/> interface.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class FileStorage : IStorage
{
    private const long InvalidSize = -1;

    private SharedRef<IFile> _baseFileShared;
    private IFile _baseFile;
    private long _fileSize;

    protected FileStorage()
    {
        _fileSize = InvalidSize;
    }

    public FileStorage(IFile baseFile)
    {
        _baseFile = baseFile;
        _fileSize = InvalidSize;
    }

    public FileStorage(ref SharedRef<IFile> baseFile)
    {
        _baseFile = baseFile.Get;
        _baseFileShared = SharedRef<IFile>.CreateMove(ref baseFile);
        _fileSize = InvalidSize;
    }

    public override void Dispose()
    {
        _baseFileShared.Destroy();
        base.Dispose();
    }

    protected void SetFile(IFile file)
    {
        Assert.SdkRequiresNotNull(file);
        Assert.SdkRequiresNull(_baseFile);

        _baseFile = file;
    }

    protected override Result DoRead(long offset, Span<byte> destination)
    {
        if (destination.Length == 0)
            return Result.Success;

        Result rc = UpdateSize();
        if (rc.IsFailure()) return rc.Miss();

        if (!CheckAccessRange(offset, destination.Length, _fileSize))
            return ResultFs.OutOfRange.Log();

        return _baseFile.Read(out _, offset, destination, ReadOption.None);
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
    {
        if (source.Length == 0)
            return Result.Success;

        Result rc = UpdateSize();
        if (rc.IsFailure()) return rc.Miss();

        if (!CheckAccessRange(offset, source.Length, _fileSize))
            return ResultFs.OutOfRange.Log();

        return _baseFile.Write(offset, source, WriteOption.None);
    }

    protected override Result DoFlush()
    {
        return _baseFile.Flush();
    }

    protected override Result DoGetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result rc = UpdateSize();
        if (rc.IsFailure()) return rc.Miss();

        size = _fileSize;
        return Result.Success;
    }

    protected override Result DoSetSize(long size)
    {
        _fileSize = InvalidSize;
        return _baseFile.SetSize(size);
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        if (operationId == OperationId.InvalidateCache)
        {
            Result rc = _baseFile.OperateRange(OperationId.InvalidateCache, offset, size);
            if (rc.IsFailure()) return rc.Miss();

            return Result.Success;
        }

        if (operationId == OperationId.QueryRange)
        {
            if (size == 0)
            {
                if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
                    return ResultFs.InvalidSize.Log();

                SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer).Clear();
                return Result.Success;
            }

            Result rc = UpdateSize();
            if (rc.IsFailure()) return rc.Miss();

            if (!CheckOffsetAndSize(offset, size))
                return ResultFs.OutOfRange.Log();

            return _baseFile.OperateRange(outBuffer, operationId, offset, size, inBuffer);
        }

        return ResultFs.UnsupportedOperateRangeForFileStorage.Log();
    }

    private Result UpdateSize()
    {
        if (_fileSize != InvalidSize)
            return Result.Success;

        Result rc = _baseFile.GetSize(out long size);
        if (rc.IsFailure()) return rc.Miss();

        _fileSize = size;
        return Result.Success;
    }
}

/// <summary>
/// Opens a file from an <see cref="IFileSystem"/> and allows interacting with it through an
/// <see cref="IStorage"/> interface. The opened file will automatically be closed when the
/// <see cref="FileStorageBasedFileSystem"/> is disposed.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class FileStorageBasedFileSystem : FileStorage
{
    private SharedRef<IFileSystem> _baseFileSystem;
    private UniqueRef<IFile> _baseFile;

    public override void Dispose()
    {
        _baseFile.Destroy();
        _baseFileSystem.Destroy();

        base.Dispose();
    }

    /// <summary>
    /// Initializes this <see cref="FileStorageBasedFileSystem"/> with the file at the specified path.
    /// </summary>
    /// <param name="baseFileSystem">The <see cref="IFileSystem"/> containing the file to open.</param>
    /// <param name="path">The full path of the file to open.</param>
    /// <param name="mode">Specifies the access permissions of the opened file.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.PathNotFound"/>: The specified path does not exist or is a directory.<br/>
    /// <see cref="ResultFs.TargetLocked"/>: When opening as <see cref="OpenMode.Write"/>,
    /// the file is already opened as <see cref="OpenMode.Write"/>.</returns>
    public Result Initialize(ref SharedRef<IFileSystem> baseFileSystem, in Path path, OpenMode mode)
    {
        using var baseFile = new UniqueRef<IFile>();

        Result rc = baseFileSystem.Get.OpenFile(ref baseFile.Ref(), in path, mode);
        if (rc.IsFailure()) return rc.Miss();

        SetFile(baseFile.Get);
        _baseFileSystem.SetByMove(ref baseFileSystem);
        _baseFile.Set(ref baseFile.Ref());

        return Result.Success;
    }
}

/// <summary>
/// Provides an <see cref="IStorage"/> interface for interacting with an opened file from a mounted file system.
/// The caller may choose whether or not the file will be closed when the <see cref="FileHandleStorage"/> is disposed.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class FileHandleStorage : IStorage
{
    private const long InvalidSize = -1;

    private FileHandle _handle;
    private bool _closeFile;
    private long _size;
    private SdkMutexType _mutex;

    // LibHac addition because we don't use global state for the FS client
    private FileSystemClient _fsClient;

    /// <summary>
    /// Initializes a new <see cref="FileHandleStorage"/> with the provided <see cref="FileHandle"/>.
    /// The file will not be closed when this <see cref="FileHandleStorage"/> is disposed.
    /// </summary>
    /// <param name="fsClient">The <see cref="FileSystemClient"/> of the provided <see cref="FileHandle"/>.</param>
    /// <param name="handle">The handle of the file to use.</param>
    public FileHandleStorage(FileSystemClient fsClient, FileHandle handle) : this(fsClient, handle, false) { }

    /// <summary>
    /// Initializes a new <see cref="FileHandleStorage"/> with the provided <see cref="FileHandle"/>.
    /// </summary>
    /// <param name="fsClient">The <see cref="FileSystemClient"/> of the provided <see cref="FileHandle"/>.</param>
    /// <param name="handle">The handle of the file to use.</param>
    /// <param name="closeFile">Should <paramref name="handle"/> be closed when this
    /// <see cref="FileHandleStorage"/> is disposed?</param>
    public FileHandleStorage(FileSystemClient fsClient, FileHandle handle, bool closeFile)
    {
        _fsClient = fsClient;
        _handle = handle;
        _closeFile = closeFile;
        _size = InvalidSize;
        _mutex.Initialize();
    }

    public override void Dispose()
    {
        if (_closeFile)
        {
            _fsClient.CloseFile(_handle);
            _closeFile = false;
            _handle = default;
        }

        base.Dispose();
    }

    protected override Result DoRead(long offset, Span<byte> destination)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (destination.Length == 0)
            return Result.Success;

        Result rc = UpdateSize();
        if (rc.IsFailure()) return rc.Miss();

        if (!CheckAccessRange(offset, destination.Length, _size))
            return ResultFs.OutOfRange.Log();

        return _fsClient.ReadFile(_handle, offset, destination, ReadOption.None);
    }

    protected override Result DoWrite(long offset, ReadOnlySpan<byte> source)
    {
        using ScopedLock<SdkMutexType> scopedLock = ScopedLock.Lock(ref _mutex);

        if (source.Length == 0)
            return Result.Success;

        Result rc = UpdateSize();
        if (rc.IsFailure()) return rc.Miss();

        if (!CheckAccessRange(offset, source.Length, _size))
            return ResultFs.OutOfRange.Log();

        return _fsClient.WriteFile(_handle, offset, source, WriteOption.None);
    }

    protected override Result DoFlush()
    {
        return _fsClient.FlushFile(_handle);
    }

    protected override Result DoGetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        Result rc = UpdateSize();
        if (rc.IsFailure()) return rc.Miss();

        size = _size;
        return Result.Success;
    }

    protected override Result DoSetSize(long size)
    {
        _size = InvalidSize;
        return _fsClient.SetFileSize(_handle, size);
    }

    protected override Result DoOperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        if (operationId != OperationId.QueryRange)
            return ResultFs.UnsupportedOperateRangeForFileHandleStorage.Log();

        if (outBuffer.Length != Unsafe.SizeOf<QueryRangeInfo>())
            return ResultFs.InvalidSize.Log();

        Result rc = _fsClient.QueryRange(out SpanHelpers.AsStruct<QueryRangeInfo>(outBuffer), _handle, offset, size);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    private Result UpdateSize()
    {
        if (_size != InvalidSize)
            return Result.Success;

        Result rc = _fsClient.GetFileSize(out long size, _handle);
        if (rc.IsFailure()) return rc.Miss();

        _size = size;
        return Result.Success;
    }
}