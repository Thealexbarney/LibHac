using System;
using LibHac.Common;
using LibHac.Diag;

namespace LibHac.Fs;

/// <summary>
/// Presents a subsection of a base IStorage as a new IStorage.
/// </summary>
/// <remarks>
/// <para>A SubStorage presents a sub-range of an IStorage as a separate IStorage.</para>
///
/// <para>The SubStorage doesn't check if the offset and size provided are actually in the base storage.
/// GetSize will return the size given to the SubStorage at initialization and will not query
/// the base storage's size.</para>
///
/// <para>A SubStorage is non-resizable by default. <see cref="SetResizable"/> may be used to mark
/// the SubStorage as resizable. The SubStorage may only be resized if the end of the SubStorage
/// is located at the end of the base storage. When resizing the SubStorage, the base storage
/// will be resized to the appropriate length.</para>
/// <para>Based on FS 14.1.0 (nnSdk 14.3.0)</para>
/// </remarks>
public class SubStorage : IStorage
{
    private SharedRef<IStorage> _sharedBaseStorage;
    protected IStorage BaseStorage;
    private long _offset;
    private long _size;
    private bool _isResizable;

    /// <summary>
    /// Creates an uninitialized <see cref="SubStorage"/>. It must be initialized with <see cref="Set"/> before using.
    /// </summary>
    public SubStorage()
    {
        BaseStorage = null;
        _offset = 0;
        _size = 0;
        _isResizable = false;
    }

    /// <summary>
    /// Initializes a new <see cref="SubStorage"/> as a copy of <paramref name="other"/>.
    /// <paramref name="other"/> will not be disposed when the created <see cref="SubStorage"/> is disposed.
    /// </summary>
    /// <param name="other">The <see cref="SubStorage"/> to create a copy of.</param>
    public SubStorage(SubStorage other)
    {
        BaseStorage = other.BaseStorage;
        _offset = other._offset;
        _size = other._size;
        _isResizable = other._isResizable;
        _sharedBaseStorage = SharedRef<IStorage>.CreateCopy(in other._sharedBaseStorage);
    }

    /// <summary>
    /// Creates a <see cref="SubStorage"/> from a subsection of another <see cref="IStorage"/>.
    /// <paramref name="baseStorage"/> will not be disposed when the created <see cref="SubStorage"/> is disposed.
    /// </summary>
    /// <param name="baseStorage">The base <see cref="IStorage"/>. Caller retains ownership.</param>
    /// <param name="offset">The offset in the base storage at which to begin the created SubStorage.</param>
    /// <param name="size">The size of the created SubStorage.</param>
    public SubStorage(IStorage baseStorage, long offset, long size)
    {
        BaseStorage = baseStorage;
        _offset = offset;
        _size = size;
        _isResizable = false;

        Assert.SdkRequiresNotNull(baseStorage);
        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequiresLessEqual(0, size);
    }

    /// <summary>
    /// Creates a <see cref="SubStorage"/> from a subsection of another <see cref="SubStorage"/>.
    /// <paramref name="other"/> will not be disposed when the created <see cref="SubStorage"/> is disposed.
    /// Any shared references to the base <see cref="IStorage"/> will be copied over.
    /// </summary>
    /// <remarks>
    /// The created SubStorage will directly use the base <see cref="IStorage"/> of <paramref name="other"/>
    /// and will adjust the <paramref name="offset"/> and <paramref name="size"/> accordingly.
    /// This avoids the overhead of going through two SubStorage layers.
    /// </remarks>
    /// <param name="other">The base SubStorage.</param>
    /// <param name="offset">The offset in the base storage at which to begin the created SubStorage.</param>
    /// <param name="size">The size of the SubStorage.</param>
    public SubStorage(SubStorage other, long offset, long size)
    {
        BaseStorage = other.BaseStorage;
        _offset = other._offset + offset;
        _size = size;
        _isResizable = false;
        _sharedBaseStorage = SharedRef<IStorage>.CreateCopy(in other._sharedBaseStorage);

        Assert.SdkRequiresLessEqual(0, offset);
        Assert.SdkRequiresLessEqual(0, size);
        Assert.SdkRequires(other.IsValid());
        Assert.SdkRequiresGreaterEqual(other._size, offset + size);
    }

    /// <summary>
    /// Creates a <see cref="SubStorage"/> from a subsection of another <see cref="IStorage"/>.
    /// Holds a reference to <paramref name="baseStorage"/> until disposed.
    /// </summary>
    /// <param name="baseStorage">The base <see cref="IStorage"/>.</param>
    /// <param name="offset">The offset in the base storage at which to begin the created SubStorage.</param>
    /// <param name="size">The size of the created SubStorage.</param>
    public SubStorage(in SharedRef<IStorage> baseStorage, long offset, long size)
    {
        BaseStorage = baseStorage.Get;
        _offset = offset;
        _size = size;
        _isResizable = false;
        _sharedBaseStorage = SharedRef<IStorage>.CreateCopy(in baseStorage);

        Assert.SdkRequiresNotNull(baseStorage.Get);
        Assert.SdkRequiresLessEqual(0, _offset);
        Assert.SdkRequiresLessEqual(0, _size);
    }

    public override void Dispose()
    {
        _sharedBaseStorage.Destroy();
        base.Dispose();
    }

    /// <summary>
    /// Sets this <see cref="SubStorage"/> to be a copy of <paramref name="other"/>.
    /// Any shared references in <paramref name="other"/> will be copied.
    /// </summary>
    /// <param name="other">The <see cref="SubStorage"/> used to initialize this one.</param>
    public void Set(SubStorage other)
    {
        if (this != other)
        {
            BaseStorage = other.BaseStorage;
            _offset = other._offset;
            _size = other._size;
            _isResizable = other._isResizable;
            _sharedBaseStorage.SetByCopy(in other._sharedBaseStorage);
        }
    }

    private bool IsValid() => BaseStorage != null;

    /// <summary>
    /// Sets whether the <see cref="SubStorage"/> is resizable or not.
    /// </summary>
    /// <param name="isResizable"><see langword="true"/> if the <see cref="SubStorage"/> should
    /// be resizable. <see langword="false"/> if not.</param>
    public void SetResizable(bool isResizable)
    {
        _isResizable = isResizable;
    }

    public override Result Read(long offset, Span<byte> destination)
    {
        if (!IsValid()) return ResultFs.NotInitialized.Log();
        if (destination.Length == 0) return Result.Success;

        Result rc = CheckAccessRange(offset, destination.Length, _size);
        if (rc.IsFailure()) return rc.Miss();

        rc = BaseStorage.Read(_offset + offset, destination);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result Write(long offset, ReadOnlySpan<byte> source)
    {
        if (!IsValid()) return ResultFs.NotInitialized.Log();
        if (source.Length == 0) return Result.Success;

        Result rc = CheckAccessRange(offset, source.Length, _size);
        if (rc.IsFailure()) return rc.Miss();

        rc = BaseStorage.Write(_offset + offset, source);
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result Flush()
    {
        if (!IsValid()) return ResultFs.NotInitialized.Log();

        Result rc = BaseStorage.Flush();
        if (rc.IsFailure()) return rc.Miss();

        return Result.Success;
    }

    public override Result SetSize(long size)
    {
        if (!IsValid()) return ResultFs.NotInitialized.Log();
        if (!_isResizable) return ResultFs.UnsupportedSetSizeForNotResizableSubStorage.Log();

        Result rc = CheckOffsetAndSize(_offset, size);
        if (rc.IsFailure()) return rc.Miss();

        rc = BaseStorage.GetSize(out long currentSize);
        if (rc.IsFailure()) return rc;

        if (currentSize != _offset + _size)
        {
            // SubStorage cannot be resized unless it is located at the end of the base storage.
            return ResultFs.UnsupportedSetSizeForResizableSubStorage.Log();
        }

        rc = BaseStorage.SetSize(_offset + size);
        if (rc.IsFailure()) return rc;

        _size = size;

        return Result.Success;
    }

    public override Result GetSize(out long size)
    {
        UnsafeHelpers.SkipParamInit(out size);

        if (!IsValid()) return ResultFs.NotInitialized.Log();

        size = _size;
        return Result.Success;
    }

    public override Result OperateRange(Span<byte> outBuffer, OperationId operationId, long offset, long size, ReadOnlySpan<byte> inBuffer)
    {
        if (!IsValid()) return ResultFs.NotInitialized.Log();

        if (operationId != OperationId.InvalidateCache)
        {
            if (size == 0) return Result.Success;

            Result rc = CheckOffsetAndSize(_offset, size);
            if (rc.IsFailure()) return rc.Miss();
        }

        return BaseStorage.OperateRange(outBuffer, operationId, _offset + offset, size, inBuffer);
    }
}