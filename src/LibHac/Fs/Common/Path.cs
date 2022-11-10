using System;
using System.Buffers;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Util;
using static InlineIL.IL.Emit;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs;

public struct PathFlags
{
    private uint _value;

    public void AllowWindowsPath() => _value |= 1 << 0;
    public void AllowRelativePath() => _value |= 1 << 1;
    public void AllowEmptyPath() => _value |= 1 << 2;
    public void AllowMountName() => _value |= 1 << 3;
    public void AllowBackslash() => _value |= 1 << 4;
    public void AllowAllCharacters() => _value |= 1 << 5;

    public readonly bool IsWindowsPathAllowed() => (_value & (1 << 0)) != 0;
    public readonly bool IsRelativePathAllowed() => (_value & (1 << 1)) != 0;
    public readonly bool IsEmptyPathAllowed() => (_value & (1 << 2)) != 0;
    public readonly bool IsMountNameAllowed() => (_value & (1 << 3)) != 0;
    public readonly bool IsBackslashAllowed() => (_value & (1 << 4)) != 0;
    public readonly bool AreAllCharactersAllowed() => (_value & (1 << 5)) != 0;
}

/// <summary>
/// Contains functions like those in <see cref="System.Runtime.CompilerServices.Unsafe"/> because ref struct
/// types can't be used as generics yet.
/// </summary>
public static class PathExtensions
{
    /// <summary>
    /// Reinterprets the given read-only reference as a reference.
    /// </summary>
    /// <remarks><para>This function allows using a <see langword="using"/> expression with <see cref="Path"/>s
    /// while still being able to pass it by reference.</para>
    /// <para>This function is a static method instead of an instance method because
    /// as a static method we get escape analysis so the lifetime of the returned reference is restricted to that
    /// of the input <see langword="readonly"/> reference.</para></remarks>
    /// <param name="path">The read-only reference to reinterpret.</param>
    /// <returns>A reference to the given <see cref="Path"/>.</returns>
    // ReSharper disable once EntityNameCapturedOnly.Global
    public static ref Path Ref(this in Path path)
    {
        Ldarg(nameof(path));
        Ret();
        throw InlineIL.IL.Unreachable();
    }

    public static ref Path GetNullRef()
    {
        Ldc_I4_0();
        Conv_U();
        Ret();
        throw InlineIL.IL.Unreachable();
    }

    public static bool IsNullRef(in Path path)
    {
        Ldarg_0();
        Ldc_I4_0();
        Conv_U();
        Ceq();
        return InlineIL.IL.Return<bool>();
    }
}

/// <summary>
/// Represents a file path stored as a UTF-8 string.
/// </summary>
/// <remarks>
/// <para>A <see cref="Path"/> has three parts to it:<br/>
/// 1. A <see cref="byte"/> <see cref="ReadOnlySpan{T}"/> that points to the current path string.<br/>
/// 2. A write buffer that can be allocated if operations need to be done on the path.<br/>
/// 3. An <c>IsNormalized</c> flag that tracks the path normalization status of the current path.</para>
/// <para>There are two different ways to initialize a <see cref="Path"/>. The "Initialize*" methods will
/// ensure a write buffer is allocated and copy the input path to it. <see cref="SetShallowBuffer"/> will
/// directly use the input buffer without copying. If this method is used, the caller must ensure the path
/// is normalized before passing it to <see cref="SetShallowBuffer"/>.</para>
/// <para>Based on nnSdk 13.4.0 (FS 13.1.0)</para></remarks>
[DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
[NonCopyableDisposable]
public ref struct Path
{
    /// <summary>
    /// Used to store a path in a non-ref struct.
    /// </summary>
    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public struct Stored : IDisposable
    {
        private static readonly byte[] EmptyBuffer = { 0 };

        private byte[] _buffer;
        private int _length;

        public Stored()
        {
            _buffer = EmptyBuffer;
            _length = 0;
        }

        public void Dispose()
        {
            byte[] buffer = Shared.Move(ref _buffer);
            if (buffer is not null)
            {
                ArrayPool<byte>.Shared.Return(buffer);
            }
        }

        /// <summary>
        /// Initializes this <see cref="Stored"/> path with the data from a standard <see cref="Path"/>.
        /// <paramref name="path"/> must be normalized.
        /// </summary>
        /// <param name="path">The <see cref="Path"/> used to initialize this one.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.NotNormalized"/>: The <c>IsNormalized</c> flag of
        /// <paramref name="path"/> is not <see langword="true"/>.</returns>
        public Result Initialize(in Path path)
        {
            if (!path._isNormalized)
                return ResultFs.NotNormalized.Log();

            _length = path.GetLength();

            Result rc = Preallocate(_length + NullTerminatorLength);
            if (rc.IsFailure()) return rc;

            int bytesCopied = StringUtils.Copy(_buffer, path._string, _length + NullTerminatorLength);

            if (bytesCopied != _length)
                return ResultFs.UnexpectedInPathA.Log();

            return Result.Success;
        }

        public readonly int GetLength() => _length;
        public readonly ReadOnlySpan<byte> GetString() => _buffer;

        /// <summary>
        /// Creates a <see cref="Path"/> from this <see cref="Path.Stored"/>. This <see cref="Stored"/>
        /// must not be reinitialized or disposed for the lifetime of the created <see cref="Path"/>.
        /// </summary>
        /// <returns>The created <see cref="Path"/>.</returns>
        public readonly Path DangerousGetPath()
        {
            return new Path
            {
                _string = _buffer,
                _isNormalized = true
            };
        }

        private Result Preallocate(int length)
        {
            if (_buffer is not null && _buffer.Length > length)
                return Result.Success;

            int alignedLength = Alignment.AlignUpPow2(length, WriteBufferAlignmentLength);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(alignedLength);

            byte[] oldBuffer = _buffer;
            _buffer = buffer;

            // Check if the buffer is longer than 1 so we don't try to return EmptyBuffer to the pool.
            if (oldBuffer?.Length > 1)
                ArrayPool<byte>.Shared.Return(oldBuffer);

            return Result.Success;
        }

        public override string ToString() => StringUtils.Utf8ZToString(_buffer);
    }

    private const int SeparatorLength = 1;
    private const int NullTerminatorLength = 1;
    private const int WriteBufferAlignmentLength = 8;
    private static ReadOnlySpan<byte> EmptyPath => new byte[] { 0 };

    private ReadOnlySpan<byte> _string;
    private byte[] _writeBuffer;
    private int _writeBufferLength;
    private bool _isNormalized;

    public Path()
    {
        _string = EmptyPath;
        _writeBuffer = null;
        _writeBufferLength = 0;
        _isNormalized = false;
    }

    public void Dispose()
    {
        byte[] writeBuffer = Shared.Move(ref _writeBuffer);
        if (writeBuffer is not null)
        {
            ArrayPool<byte>.Shared.Return(writeBuffer);
        }
    }

    /// <summary>
    /// Gets the current write buffer.
    /// </summary>
    /// <returns>The write buffer.</returns>
    internal Span<byte> GetWriteBuffer()
    {
        Assert.SdkRequires(_writeBuffer is not null);
        return _writeBuffer.AsSpan();
    }

    /// <summary>
    /// Gets the current length of the write buffer.
    /// </summary>
    /// <returns>The write buffer length.</returns>
    internal readonly long GetWriteBufferLength()
    {
        return _writeBufferLength;
    }

    /// <summary>
    /// Calculates the length of the current string.
    /// </summary>
    /// <returns>The length of the current string></returns>
    public readonly int GetLength()
    {
        return StringUtils.GetLength(GetString());
    }

    /// <summary>
    /// Returns <see langword="true"/> if the <see cref="Path"/> has no buffer or has an empty string.
    /// </summary>
    /// <returns><see langword="true"/> if the current path is empty; otherwise <see langword="false"/>.</returns>
    public readonly bool IsEmpty()
    {
        return _string.At(0) == 0;
    }

    /// <summary>
    /// Calculates if the first "<paramref name="length"/>" characters of the
    /// current path and <paramref name="value"/> are the same.
    /// </summary>
    /// <param name="value">The string to compare to this <see cref="Path"/>.</param>
    /// <param name="length">The maximum number of characters to compare.</param>
    /// <returns><see langword="true"/> if the strings are the same; otherwise <see langword="false"/>.</returns>
    public readonly bool IsMatchHead(ReadOnlySpan<byte> value, int length)
    {
        return StringUtils.Compare(GetString(), value, length) == 0;
    }

    public static bool operator !=(in Path left, in Path right)
    {
        return !(left == right);
    }

    public static bool operator !=(in Path left, ReadOnlySpan<byte> right)
    {
        return !(left == right);
    }

    public static bool operator ==(in Path left, in Path right)
    {
        return StringUtils.Compare(left.GetString(), right.GetString()) == 0;
    }

    public static bool operator ==(in Path left, ReadOnlySpan<byte> right)
    {
        return StringUtils.Compare(left.GetString(), right) == 0;
    }

    /// <summary>
    /// Releases this <see cref="Path"/>'s write buffer and returns it to the caller.
    /// </summary>
    /// <returns>The write buffer if the <see cref="Path"/> had one; otherwise <see langword="null"/>.</returns>
    public byte[] ReleaseBuffer()
    {
        Assert.SdkRequires(_writeBuffer is not null);

        _string = EmptyPath;
        _writeBufferLength = 0;

        return Shared.Move(ref _writeBuffer);
    }

    /// <summary>
    /// Releases any current write buffer and sets this <see cref="Path"/> to an empty string.
    /// </summary>
    private void ClearBuffer()
    {
        byte[] oldBuffer = Shared.Move(ref _writeBuffer);

        if (oldBuffer is not null)
            ArrayPool<byte>.Shared.Return(oldBuffer);

        _writeBufferLength = 0;
        _string = EmptyPath;
    }

    /// <summary>
    /// Releases any current write buffer and sets the provided buffer as the new write buffer.
    /// </summary>
    /// <param name="buffer">The new write buffer.</param>
    /// <param name="length">The length of the write buffer.
    /// Must be a multiple of <see cref="WriteBufferAlignmentLength"/>.</param>
    private void SetModifiableBuffer(byte[] buffer, int length)
    {
        Assert.SdkRequiresNotNull(buffer);
        Assert.SdkRequires(length > 0);
        Assert.SdkRequires(Alignment.IsAlignedPow2(length, WriteBufferAlignmentLength));

        byte[] oldBuffer = _writeBuffer;
        _writeBuffer = buffer;

        if (oldBuffer is not null)
            ArrayPool<byte>.Shared.Return(oldBuffer);

        _writeBufferLength = length;
        _string = buffer;
    }

    /// <summary>
    /// Releases any current write buffer and sets <paramref name="buffer"/> as this <see cref="Path"/>'s string.
    /// </summary>
    /// <param name="buffer">The buffer containing the new path.</param>
    private void SetReadOnlyBuffer(ReadOnlySpan<byte> buffer)
    {
        _string = buffer;

        byte[] oldBuffer = Shared.Move(ref _writeBuffer);

        if (oldBuffer is not null)
            ArrayPool<byte>.Shared.Return(oldBuffer);

        _writeBufferLength = 0;
    }

    /// <summary>
    /// Ensures the write buffer is the specified <paramref name="length"/> or larger.
    /// </summary>
    /// <param name="length">The minimum desired length.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    private Result Preallocate(int length)
    {
        if (_writeBufferLength > length)
            return Result.Success;

        int alignedLength = Alignment.AlignUpPow2(length, WriteBufferAlignmentLength);
        byte[] buffer = ArrayPool<byte>.Shared.Rent(alignedLength);
        SetModifiableBuffer(buffer, alignedLength);

        return Result.Success;
    }

    /// <summary>
    /// Releases any current write buffer and sets <paramref name="buffer"/> as this <see cref="Path"/>'s string.<br/>
    /// The path contained by <paramref name="buffer"/> must be normalized.
    /// </summary>
    /// <remarks>It is up to the caller to ensure the path contained by <paramref name="buffer"/> is normalized.
    /// This function will always set the <c>IsNormalized</c> flag to <see langword="true"/>.</remarks>
    /// <param name="buffer">The buffer containing the new path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result SetShallowBuffer(ReadOnlySpan<byte> buffer)
    {
        Assert.SdkRequires(_writeBufferLength == 0);

        SetReadOnlyBuffer(buffer);
        _isNormalized = true;
        return Result.Success;
    }

    /// <summary>
    /// Gets the buffer containing the current path. 
    /// </summary>
    /// <remarks>This <see cref="Path"/>'s <c>IsNormalized</c> flag should be
    /// <see langword="true"/> before calling this function.</remarks>
    /// <returns>The buffer containing the current path.</returns>
    public readonly ReadOnlySpan<byte> GetString()
    {
        Assert.SdkAssert(_isNormalized);

        return _string;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> with the data from another Path.<br/>
    /// <paramref name="other"/> must be normalized.
    /// </summary>
    /// <remarks>This <see cref="Path"/>'s <c>IsNormalized</c> flag will be set to
    /// the value of <paramref name="other"/>'s flag.</remarks>
    /// <param name="other">The <see cref="Path"/> used to initialize this one.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.NotNormalized"/>: The <c>IsNormalized</c> flag of
    /// <paramref name="other"/> is not <see langword="true"/>.</returns>
    public Result Initialize(in Path other)
    {
        if (!other._isNormalized)
            return ResultFs.NotNormalized.Log();

        int otherLength = other.GetLength();

        Result rc = Preallocate(otherLength + NullTerminatorLength);
        if (rc.IsFailure()) return rc;

        int bytesCopied = StringUtils.Copy(_writeBuffer, other.GetString(), otherLength + NullTerminatorLength);

        if (bytesCopied != otherLength)
            return ResultFs.UnexpectedInPathA.Log();

        _isNormalized = other._isNormalized;
        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> with the data from a <see cref="Stored"/> path.
    /// </summary>
    /// <remarks>Ensures we have a large enough write buffer and copies the path to it.
    /// This function always sets the <c>IsNormalized</c> flag to <see langword="true"/>
    /// because <see cref="Stored"/> paths are always normalized upon initialization.</remarks>
    /// <param name="other">The <see cref="Stored"/> path used to initialize this <see cref="Path"/>.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result Initialize(in Stored other)
    {
        int otherLength = other.GetLength();

        Result rc = Preallocate(otherLength + NullTerminatorLength);
        if (rc.IsFailure()) return rc;

        int bytesCopied = StringUtils.Copy(_writeBuffer, other.GetString(), otherLength + NullTerminatorLength);

        if (bytesCopied != otherLength)
            return ResultFs.UnexpectedInPathA.Log();

        _isNormalized = true;
        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer.
    /// </summary>
    /// <remarks>Ensures the write buffer is large enough to hold <paramref name="path"/>
    /// and copies <paramref name="path"/> to the write buffer.<br/>
    /// This function does not modify the <c>IsNormalized</c> flag.</remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <param name="length">The length of the provided path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    private Result InitializeImpl(ReadOnlySpan<byte> path, int length)
    {
        if (length == 0 || path.At(0) == NullTerminator)
        {
            ClearBuffer();
            return Result.Success;
        }

        Result rc = Preallocate(length + NullTerminatorLength);
        if (rc.IsFailure()) return rc;

        int bytesCopied = StringUtils.Copy(GetWriteBuffer(), path, length + NullTerminatorLength);

        if (bytesCopied < length)
            return ResultFs.UnexpectedInPathA.Log();

        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer.
    /// </summary>
    /// <remarks>Ensures the write buffer is large enough to hold <paramref name="path"/>
    /// and copies <paramref name="path"/> to the write buffer.<br/>
    /// This function will always set the <c>IsNormalized</c> flag to <see langword="false"/>.</remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result Initialize(ReadOnlySpan<byte> path)
    {
        Result rc = InitializeImpl(path, StringUtils.GetLength(path));
        if (rc.IsFailure()) return rc;

        _isNormalized = false;
        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer and
    /// normalizes it if the path is a relative path or a Windows path.
    /// </summary>
    /// <remarks>This function normalizes relative paths and Windows paths but does not normalize any other paths,
    /// although all paths are checked for invalid characters and if the path is in a valid format.<br/>
    /// The <c>IsNormalized</c> flag will always be set to <see langword="true"/> even if the incoming path
    /// is not normalized. This can lead to a situation where the path is not normalized yet the
    /// <c>IsNormalized</c> flag is still <see langword="true"/>.</remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidCharacter"/>: The path contains an invalid character.<br/>
    /// <see cref="ResultFs.InvalidPathFormat"/>: The path is not in a valid format.</returns>
    public Result InitializeWithNormalization(ReadOnlySpan<byte> path)
    {
        Result rc = Initialize(path);
        if (rc.IsFailure()) return rc;

        if (_string.At(0) != NullTerminator && !WindowsPath.IsWindowsPath(_string, false) &&
            _string.At(0) != DirectorySeparator)
        {
            var flags = new PathFlags();
            flags.AllowRelativePath();

            rc = Normalize(flags);
            if (rc.IsFailure()) return rc;
        }
        else if (WindowsPath.IsWindowsPath(_string, true))
        {
            var flags = new PathFlags();
            flags.AllowWindowsPath();

            rc = Normalize(flags);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            rc = PathNormalizer.IsNormalized(out _isNormalized, out _, _string);
            if (rc.IsFailure()) return rc;
        }

        // Note: I have no idea why Nintendo checks if the path is normalized
        // and then unconditionally sets _isNormalized to true right after.
        // Maybe it's a mistake and somehow nobody noticed?
        _isNormalized = true;
        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer.
    /// </summary>
    /// <remarks>Ensures the write buffer is large enough to hold <paramref name="path"/>
    /// and copies <paramref name="path"/> to the write buffer.<br/>
    /// This function will always set the <c>IsNormalized</c> flag to <see langword="false"/>.</remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <param name="length">The length of the provided path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result Initialize(ReadOnlySpan<byte> path, int length)
    {
        Result rc = InitializeImpl(path, length);
        if (rc.IsFailure()) return rc;

        _isNormalized = false;
        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer and
    /// normalizes it if the path is a relative path or a Windows path.
    /// </summary>
    /// <remarks>This function normalizes relative paths and Windows paths but does not normalize any other paths,
    /// although all paths are checked for invalid characters and if the path is in a valid format.<br/>
    /// The <c>IsNormalized</c> flag will always be set to <see langword="true"/> even if the incoming path
    /// is not normalized. This can lead to a situation where the path is not normalized yet the
    /// <c>IsNormalized</c> flag is still <see langword="true"/>.</remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <param name="length">The length of the provided path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidCharacter"/>: The path contains an invalid character.<br/>
    /// <see cref="ResultFs.InvalidPathFormat"/>: The path is not in a valid format.</returns>
    public Result InitializeWithNormalization(ReadOnlySpan<byte> path, int length)
    {
        Result rc = Initialize(path, length);
        if (rc.IsFailure()) return rc;

        if (_string.At(0) != NullTerminator && !WindowsPath.IsWindowsPath(_string, false) &&
            _string.At(0) != DirectorySeparator)
        {
            var flags = new PathFlags();
            flags.AllowRelativePath();

            rc = Normalize(flags);
            if (rc.IsFailure()) return rc;
        }
        else if (WindowsPath.IsWindowsPath(_string, true))
        {
            var flags = new PathFlags();
            flags.AllowWindowsPath();

            rc = Normalize(flags);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            rc = PathNormalizer.IsNormalized(out _isNormalized, out _, _string);
            if (rc.IsFailure()) return rc;
        }

        // Note: I have no idea why Nintendo checks if the path is normalized
        // and then unconditionally sets _isNormalized to true right after.
        // Maybe it's a mistake and somehow nobody noticed?
        _isNormalized = true;
        return Result.Success;
    }


    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer and
    /// replaces any backslashes in the path with forward slashes.
    /// </summary>
    /// <remarks>This function will always set the <c>IsNormalized</c> flag to <see langword="false"/>.</remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result InitializeWithReplaceBackslash(ReadOnlySpan<byte> path)
    {
        Result rc = InitializeImpl(path, StringUtils.GetLength(path));
        if (rc.IsFailure()) return rc;

        if (_writeBufferLength > 1)
        {
            PathUtility.Replace(GetWriteBuffer().Slice(0, _writeBufferLength - 1), AltDirectorySeparator,
                DirectorySeparator);
        }

        _isNormalized = false;
        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer. If the path begins with two
    /// forward slashes (<c>//</c>), those two forward slashes will be replaced with two backslashes (<c>\\</c>).
    /// </summary>
    /// <remarks>This function will always set the <c>IsNormalized</c> flag to <see langword="false"/>.</remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result InitializeWithReplaceForwardSlashes(ReadOnlySpan<byte> path)
    {
        Result rc = InitializeImpl(path, StringUtils.GetLength(path));
        if (rc.IsFailure()) return rc;

        if (_writeBufferLength > 1)
        {
            Span<byte> writeBuffer = GetWriteBuffer();
            if (writeBuffer[0] == DirectorySeparator && writeBuffer[1] == DirectorySeparator)
            {
                writeBuffer[0] = AltDirectorySeparator;
                writeBuffer[1] = AltDirectorySeparator;
            }
        }

        _isNormalized = false;
        return Result.Success;
    }

    /// <summary>
    /// Initializes this <see cref="Path"/> using the path in the provided buffer
    /// and makes various UNC path-related replacements.
    /// </summary>
    /// <remarks>The following replacements are made:<br/>
    /// <c>:///</c> located anywhere in the path is replaced with <c>:/\\</c><br/>
    /// <c>@Host://</c> located at the beginning of the path is replaced with <c>@Host:\\</c><br/>
    /// <c>//</c> located at the beginning of the path is replaced with <c>\\</c>
    /// <para>This function does not modify the <c>IsNormalized</c> flag.</para>
    /// </remarks>
    /// <param name="path">The buffer containing the path to use.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result InitializeWithReplaceUnc(ReadOnlySpan<byte> path)
    {
        Result rc = InitializeImpl(path, StringUtils.GetLength(path));
        if (rc.IsFailure()) return rc;

        _isNormalized = false;

        if (path.At(0) == NullTerminator)
            return Result.Success;

        Span<byte> writeBuffer = GetWriteBuffer();

        ReadOnlySpan<byte> search = new[] { (byte)':', (byte)'/', (byte)'/', (byte)'/' }; // ":///"
        int index = StringUtils.Find(writeBuffer, search);
        if (index >= 0)
        {
            writeBuffer[index + 2] = AltDirectorySeparator;
            writeBuffer[index + 3] = AltDirectorySeparator;
        }

        ReadOnlySpan<byte> hostMountUnc = new[] // "@Host://"
            { (byte)'@', (byte)'H', (byte)'o', (byte)'s', (byte)'t', (byte)':', (byte)'/', (byte)'/' };
        if (StringUtils.Compare(writeBuffer, hostMountUnc, 8) == 0)
        {
            writeBuffer[6] = AltDirectorySeparator;
            writeBuffer[7] = AltDirectorySeparator;
        }

        if (writeBuffer.At(0) == DirectorySeparator && writeBuffer.At(1) == DirectorySeparator)
        {
            writeBuffer[0] = AltDirectorySeparator;
            writeBuffer[1] = AltDirectorySeparator;
        }

        return Result.Success;
    }

    /// <summary>
    /// Initializes the <see cref="Path"/> as an empty string.
    /// </summary>
    /// <remarks>This function will always set the <c>IsNormalized</c> flag to <see langword="true"/>.</remarks>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result InitializeAsEmpty()
    {
        ClearBuffer();
        _isNormalized = true;

        return Result.Success;
    }

    /// <summary>
    /// Updates this <see cref="Path"/> by prepending <paramref name="parent"/> to the current path.
    /// </summary>
    /// <remarks>This function does not modify the <c>IsNormalized</c> flag.
    /// If <paramref name="parent"/> is not normalized, this can lead to a situation where the resulting
    /// path is not normalized yet the <c>IsNormalized</c> flag is still <see langword="true"/>.</remarks>
    /// <param name="parent">The buffer containing the path to insert.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.NotImplemented"/>: The path provided in <paramref name="parent"/> is a Windows path.</returns>
    public Result InsertParent(ReadOnlySpan<byte> parent)
    {
        if (parent.Length == 0 || parent[0] == NullTerminator)
            return Result.Success;

        if (WindowsPath.IsWindowsPath(_string, false))
            return ResultFs.NotImplemented.Log();

        // Remove a trailing separator from the parent and a leading one from the child so we can
        // make sure there's only one separator between them when we slap them together.
        // Trim a trailing directory separator from the parent path.
        bool parentHasTrailingSlash = false;
        int parentLength = StringUtils.GetLength(parent);

        if (parent[parentLength - 1] == DirectorySeparator || parent[parentLength - 1] == AltDirectorySeparator)
        {
            parentLength--;
            parentHasTrailingSlash = true;
        }

        // Trim a leading directory separator from the current path.
        bool childHasLeadingSlash = false;
        ReadOnlySpan<byte> childPath = _string;

        if (childPath.Length != 0 && childPath[0] == DirectorySeparator)
        {
            childPath = childPath.Slice(1);
            childHasLeadingSlash = true;
        }

        int childLength = StringUtils.GetLength(childPath);

        byte[] childBuffer = null;
        try
        {
            // Get and clear our Path's current buffer.
            if (_writeBuffer is not null)
            {
                childBuffer = Shared.Move(ref _writeBuffer);
                ClearBuffer();
            }

            // Give our Path a buffer that can hold the combined string.
            Result rc = Preallocate(parentLength + DirectorySeparator + childLength + NullTerminatorLength);
            if (rc.IsFailure()) return rc;

            Span<byte> destBuffer = GetWriteBuffer();

            int childStartOffset = childHasLeadingSlash ? 1 : 0;

            if (childLength > 0)
            {
                // Copy the child part of the path to the destination buffer.
                if (childBuffer is not null)
                {
                    StringUtils.Copy(destBuffer.Slice(parentLength + SeparatorLength),
                        childBuffer.AsSpan(childStartOffset), childLength + NullTerminatorLength);
                }
                else
                {
                    Span<byte> destBuffer2 = destBuffer.Slice(childStartOffset);

                    for (int i = childLength; i > 0; i--)
                    {
                        destBuffer2[i - 1 + parentLength] = destBuffer2[i - 1];
                    }

                    destBuffer2[childLength + parentLength] = 0;
                }
            }

            // Copy the parent part of the path to the destination buffer.
            int parentBytesCopied = StringUtils.Copy(destBuffer, parent, parentLength + SeparatorLength);

            // Make sure we copied the expected number of parent bytes.
            if (!parentHasTrailingSlash)
            {
                if (parentBytesCopied != parentLength)
                    return ResultFs.UnexpectedInPathA.Log();
            }
            else if (parentBytesCopied != parentLength + SeparatorLength)
            {
                return ResultFs.UnexpectedInPathA.Log();
            }

            // Add a directory separator between the parent and child parts of the path.
            if (childLength > 0)
            {
                destBuffer[parentLength] = DirectorySeparator;
            }

            // Note: Nintendo does not reset the "_isNormalized" field on the Path.
            // This can result in the field and the actual normalization state being out of sync.

            return Result.Success;
        }
        finally
        {
            if (childBuffer is not null)
                ArrayPool<byte>.Shared.Return(childBuffer);
        }
    }

    /// <summary>
    /// Updates this <see cref="Path"/> by prepending <paramref name="parent"/> to the current path.
    /// </summary>
    /// <remarks>This function does not modify the <c>IsNormalized</c> flag.
    /// If <paramref name="parent"/> is not normalized, this can lead to a situation where the resulting
    /// path is not normalized yet the <c>IsNormalized</c> flag is still <see langword="true"/>.</remarks>
    /// <param name="parent">The <see cref="Path"/> to insert.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.NotImplemented"/>: The path provided in <paramref name="parent"/> is a Windows path.</returns>
    public Result InsertParent(in Path parent)
    {
        return InsertParent(parent.GetString());
    }

    /// <summary>
    /// Updates this <see cref="Path"/> by appending <paramref name="child"/> to the current path.
    /// </summary>
    /// <remarks>This function does not modify the <c>IsNormalized</c> flag.
    /// If <paramref name="child"/> is not normalized, this can lead to a situation where the resulting
    /// path is not normalized yet the <c>IsNormalized</c> flag is still <see langword="true"/>.</remarks>
    /// <param name="child">The buffer containing the child path to append to the current path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result AppendChild(ReadOnlySpan<byte> child)
    {
        ReadOnlySpan<byte> trimmedChild = child;

        // Trim a leading directory separator from the child path.
        if (_string.At(0) != NullTerminator)
        {
            if (trimmedChild.Length != 0 && trimmedChild[0] == DirectorySeparator)
            {
                trimmedChild = trimmedChild.Slice(1);
            }

            // Nothing to do if the child path is empty or the root directory.
            if (trimmedChild.At(0) == NullTerminator)
            {
                return Result.Success;
            }
        }

        // If our current path is empty there's nothing to append the child path to,
        // so we'll simply replace the current path with the child path.
        int parentLength = StringUtils.GetLength(_string);
        if (parentLength == 0)
        {
            return Initialize(child);
        }

        // Trim a trailing directory separator from our current path.
        if (_string[parentLength - 1] == DirectorySeparator || _string[parentLength - 1] == AltDirectorySeparator)
            parentLength--;

        int childLength = StringUtils.GetLength(trimmedChild);

        byte[] parentBuffer = null;
        try
        {
            if (_writeBuffer is not null)
            {
                parentBuffer = Shared.Move(ref _writeBuffer);
                ClearBuffer();
            }

            Result rc = Preallocate(parentLength + SeparatorLength + childLength + NullTerminatorLength);
            if (rc.IsFailure()) return rc;

            Span<byte> destBuffer = GetWriteBuffer();

            if (parentBuffer is not null && parentLength != 0)
            {
                StringUtils.Copy(destBuffer, parentBuffer, parentLength + SeparatorLength);
            }

            destBuffer[parentLength] = DirectorySeparator;

            int childBytesCopied = StringUtils.Copy(destBuffer.Slice(parentLength + 1), trimmedChild,
                childLength + NullTerminatorLength);

            if (childBytesCopied != childLength)
                return ResultFs.UnexpectedInPathA.Log();

            // Note: Nintendo does not reset the "_isNormalized" field on the Path.
            // This can result in the field and the actual normalization state being out of sync.

            return Result.Success;
        }
        finally
        {
            if (parentBuffer is not null)
                ArrayPool<byte>.Shared.Return(parentBuffer);
        }
    }

    /// <summary>
    /// Updates this <see cref="Path"/> by appending <paramref name="child"/> to the current path.
    /// </summary>
    /// <remarks>This function does not modify the <c>IsNormalized</c> flag.
    /// If <paramref name="child"/> is not normalized, this can lead to a situation where the resulting
    /// path is not normalized yet the <c>IsNormalized</c> flag is still <see langword="true"/>.</remarks>
    /// <param name="child">The child <see cref="Path"/> to append to the current path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result AppendChild(in Path child)
    {
        return AppendChild(child.GetString());
    }

    /// <summary>
    /// Combines 2 <see cref="Path"/>s into a single path.
    /// </summary>
    /// <remarks>If <paramref name="path1"/> is empty, this <see cref="Path"/>'s <c>IsNormalized</c> flag will
    /// be set to the value of <paramref name="path2"/>'s flag.
    /// Otherwise the flag will be set to the value of <paramref name="path1"/>'s flag.</remarks>
    /// <param name="path1">The first path to combine.</param>
    /// <param name="path2">The second path to combine.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.NotNormalized"/>: The <c>IsNormalized</c> flag of either
    /// <paramref name="path1"/> or <paramref name="path2"/> is not <see langword="true"/>.</returns>
    public Result Combine(in Path path1, in Path path2)
    {
        int path1Length = path1.GetLength();
        int path2Length = path2.GetLength();

        Result rc = Preallocate(path1Length + SeparatorLength + path2Length + NullTerminatorLength);
        if (rc.IsFailure()) return rc;

        rc = Initialize(in path1);
        if (rc.IsFailure()) return rc;

        if (IsEmpty())
        {
            rc = Initialize(in path2);
            if (rc.IsFailure()) return rc;
        }
        else
        {
            rc = AppendChild(in path2);
            if (rc.IsFailure()) return rc;
        }

        return Result.Success;
    }

    /// <summary>
    /// Combines a <see cref="Path"/> and a path string into a single path.
    /// </summary>
    /// <remarks>If <paramref name="path1"/> is not empty, this <see cref="Path"/>'s <c>IsNormalized</c> flag will
    /// be set to the value of <paramref name="path1"/>'s flag.
    /// Otherwise the flag will be set to <see langword="false"/>.</remarks>
    /// <param name="path1">The first path to combine.</param>
    /// <param name="path2">The second path to combine.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.NotNormalized"/>: The <c>IsNormalized</c> flag of
    /// <paramref name="path1"/> is not <see langword="true"/>.</returns>
    public Result Combine(in Path path1, ReadOnlySpan<byte> path2)
    {
        int path1Length = path1.GetLength();
        int path2Length = StringUtils.GetLength(path2);

        Result rc = Preallocate(path1Length + SeparatorLength + path2Length + NullTerminatorLength);
        if (rc.IsFailure()) return rc;

        rc = Initialize(in path1);
        if (rc.IsFailure()) return rc;

        rc = AppendChild(path2);
        if (rc.IsFailure()) return rc;

        return Result.Success;
    }

    /// <summary>
    /// Combines a path string and a <see cref="Path"/> into a single path.
    /// </summary>
    /// <remarks>This <see cref="Path"/>'s <c>IsNormalized</c> flag will
    /// always be set to <see langword="false"/>.</remarks>
    /// <param name="path1">The first path to combine.</param>
    /// <param name="path2">The second path to combine.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.</returns>
    public Result Combine(ReadOnlySpan<byte> path1, in Path path2)
    {
        int path1Length = StringUtils.GetLength(path1);
        int path2Length = path2.GetLength();

        Result rc = Preallocate(path1Length + SeparatorLength + path2Length + NullTerminatorLength);
        if (rc.IsFailure()) return rc;

        rc = Initialize(path1);
        if (rc.IsFailure()) return rc;

        rc = AppendChild(in path2);
        if (rc.IsFailure()) return rc;

        return Result.Success;
    }

    /// <summary>
    /// Removes the last entry from this <see cref="Path"/>.
    /// </summary>
    /// <remarks>This function does not modify the <c>IsNormalized</c> flag.</remarks>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.NotImplemented"/>: The path before calling this function was
    /// one of "<c>.</c>", "<c>..</c>", "<c>/</c>" or "<c>\</c>".</returns>
    public Result RemoveChild()
    {
        // Make sure the Path has a buffer that we can write to.
        if (_writeBuffer is null)
        {
            int oldLength = StringUtils.GetLength(_string);

            if (oldLength > 0)
            {
                ReadOnlySpan<byte> oldString = _string;
                Result rc = Preallocate(oldLength);
                if (rc.IsFailure()) return rc;

                StringUtils.Copy(_writeBuffer, oldString, oldLength + NullTerminatorLength);
            }
        }

        Span<byte> path = GetWriteBuffer();
        int originalLength = StringUtils.GetLength(path);

        // We don't handle the current directory or root directory.
        if (originalLength == 1 && path[0] == DirectorySeparator)
            return ResultFs.NotImplemented.Log();

        if (originalLength == 1 && path[0] == Dot)
            return ResultFs.NotImplemented.Log();

        // Now look backward through the path for the first separator and terminate the string there.
        int currentPos = originalLength;

        // Ignore a trailing slash.
        if (originalLength > 0 &&
            (path[currentPos - 1] == DirectorySeparator || path[currentPos - 1] == AltDirectorySeparator))
        {
            currentPos--;
        }

        if (currentPos > 0)
        {
            currentPos--;

            while (currentPos >= 0 && path[currentPos] != NullTerminator)
            {
                if (path[currentPos] == DirectorySeparator || path[currentPos] == AltDirectorySeparator)
                {
                    // Don't leave a trailing slash unless the resulting path is the root directory.
                    if (currentPos == 0)
                    {
                        path[1] = NullTerminator;
                        currentPos = 1;
                    }
                    else
                    {
                        path[currentPos] = NullTerminator;
                    }

                    break;
                }

                currentPos--;
            }
        }

        if (currentPos <= 0)
            return ResultFs.NotImplemented.Log();

        return Result.Success;
    }

    /// <summary>
    /// Normalizes the current path according to the provided <paramref name="flags"/>.
    /// </summary>
    /// <remarks>If this <see cref="Path"/>'s <c>IsNormalized</c> flag is set, this function does nothing.
    /// The <c>IsNormalized</c> flag will be set if this function returns successfully.</remarks>
    /// <param name="flags">Flags that specify what types of paths are allowed.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidCharacter"/>: The path contains an invalid character.<br/>
    /// <see cref="ResultFs.InvalidPathFormat"/>: The path is in an invalid format for the specified <paramref name="flags"/>.</returns>
    public Result Normalize(PathFlags flags)
    {
        if (_isNormalized)
            return Result.Success;

        Result rc = PathFormatter.IsNormalized(out bool isNormalized, out _, _string, flags);
        if (rc.IsFailure()) return rc;

        if (isNormalized)
        {
            _isNormalized = true;
            return Result.Success;
        }

        int bufferLength = _writeBufferLength;

        if (flags.IsRelativePathAllowed() && PathUtility.IsPathRelative(_string))
            bufferLength += 2;

        if (flags.IsWindowsPathAllowed() && WindowsPath.IsWindowsPath(_string, true))
            bufferLength += 1;

        int alignedBufferLength = Alignment.AlignUpPow2(bufferLength, WriteBufferAlignmentLength);

        byte[] rentedArray = null;
        try
        {
            rentedArray = ArrayPool<byte>.Shared.Rent(alignedBufferLength);

            rc = PathFormatter.Normalize(rentedArray, GetWriteBuffer(), flags);
            if (rc.IsFailure()) return rc;

            SetModifiableBuffer(Shared.Move(ref rentedArray), alignedBufferLength);
            _isNormalized = true;
            return Result.Success;
        }
        finally
        {
            if (rentedArray is not null)
            {
                ArrayPool<byte>.Shared.Return(rentedArray);
            }
        }
    }

    public readonly override string ToString() => StringUtils.Utf8ZToString(_string);

    public override bool Equals(object obj) => throw new NotSupportedException();
    public override int GetHashCode() => throw new NotImplementedException();
}

public static class PathFunctions
{
    /// <summary>
    /// Initializes a <see cref="Path"/> with the provided basic path.
    /// The provided path must be a normalized basic path starting with a directory separator
    /// and not containing any sort of prefix such as a mount name.
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to initialize.</param>
    /// <param name="pathBuffer">The string used to initialize the <see cref="Path"/>.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidCharacter"/>: The path contains an invalid character.<br/>
    /// <see cref="ResultFs.InvalidPathFormat"/>: The path is in an invalid format or is not normalized.</returns>
    public static Result SetUpFixedPath(ref Path path, ReadOnlySpan<byte> pathBuffer)
    {
        Result rc = PathNormalizer.IsNormalized(out bool isNormalized, out _, pathBuffer);
        if (rc.IsFailure()) return rc;

        if (!isNormalized)
            return ResultFs.InvalidPathFormat.Log();

        rc = path.SetShallowBuffer(pathBuffer);
        if (rc.IsFailure()) return rc;

        return Result.Success;
    }

    // Only a small number of format strings are used with these functions, so we can hard code them all easily.

    // /%s
    /// <summary>
    /// Initializes a <see cref="Path"/> using the format string <c>/%s</c>
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to be initialized.</param>
    /// <param name="pathBuffer">The buffer that will contain the built string.</param>
    /// <param name="entryName">The first entry in the generated path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: <paramref name="pathBuffer"/> was too small to contain the built path.</returns>
    internal static Result SetUpFixedPathSingleEntry(ref Path path, Span<byte> pathBuffer,
        ReadOnlySpan<byte> entryName)
    {
        var sb = new U8StringBuilder(pathBuffer);
        sb.Append((byte)'/').Append(entryName);

        if (sb.Overflowed)
            return ResultFs.InvalidArgument.Log();

        return SetUpFixedPath(ref path, pathBuffer);
    }

    // /%s/%s
    /// <summary>
    /// Initializes a <see cref="Path"/> using the format string <c>/%s/%s</c>
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to be initialized.</param>
    /// <param name="pathBuffer">The buffer that will contain the built string.</param>
    /// <param name="entryName1">The first entry in the generated path.</param>
    /// <param name="entryName2">The second entry in the generated path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: <paramref name="pathBuffer"/> was too small to contain the built path.</returns>
    internal static Result SetUpFixedPathDoubleEntry(ref Path path, Span<byte> pathBuffer,
        ReadOnlySpan<byte> entryName1, ReadOnlySpan<byte> entryName2)
    {
        var sb = new U8StringBuilder(pathBuffer);
        sb.Append((byte)'/').Append(entryName1)
            .Append((byte)'/').Append(entryName2);

        if (sb.Overflowed)
            return ResultFs.InvalidArgument.Log();

        return SetUpFixedPath(ref path, pathBuffer);
    }

    // /%016llx
    /// <summary>
    /// Initializes a <see cref="Path"/> using the format string <c>/%016llx</c>
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to be initialized.</param>
    /// <param name="pathBuffer">The buffer that will contain the built string.</param>
    /// <param name="saveDataId">The save data ID to insert into the path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: <paramref name="pathBuffer"/> was too small to contain the built path.</returns>
    internal static Result SetUpFixedPathSaveId(ref Path path, Span<byte> pathBuffer, ulong saveDataId)
    {
        var sb = new U8StringBuilder(pathBuffer);
        sb.Append((byte)'/').AppendFormat(saveDataId, 'x', 16);

        if (sb.Overflowed)
            return ResultFs.InvalidArgument.Log();

        return SetUpFixedPath(ref path, pathBuffer);
    }

    // /%08x.meta
    /// <summary>
    /// Initializes a <see cref="Path"/> using the format string <c>/%08x.meta</c>
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to be initialized.</param>
    /// <param name="pathBuffer">The buffer that will contain the built string.</param>
    /// <param name="metaType">The <see cref="SaveDataMetaType"/> to insert into the path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: <paramref name="pathBuffer"/> was too small to contain the built path.</returns>
    internal static Result SetUpFixedPathSaveMetaName(ref Path path, Span<byte> pathBuffer, uint metaType)
    {
        ReadOnlySpan<byte> metaExtension = new[] { (byte)'.', (byte)'m', (byte)'e', (byte)'t', (byte)'a' };  // ".meta"

        var sb = new U8StringBuilder(pathBuffer);
        sb.Append((byte)'/').AppendFormat(metaType, 'x', 8).Append(metaExtension);

        if (sb.Overflowed)
            return ResultFs.InvalidArgument.Log();

        return SetUpFixedPath(ref path, pathBuffer);
    }

    // /saveMeta/%016llx
    /// <summary>
    /// Initializes a <see cref="Path"/> using the format string <c>/saveMeta/%016llx</c>
    /// </summary>
    /// <param name="path">The <see cref="Path"/> to be initialized.</param>
    /// <param name="pathBuffer">The buffer that will contain the built string.</param>
    /// <param name="saveDataId">The save data ID to insert into the path.</param>
    /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
    /// <see cref="ResultFs.InvalidArgument"/>: <paramref name="pathBuffer"/> was too small to contain the built path.</returns>
    internal static Result SetUpFixedPathSaveMetaDir(ref Path path, Span<byte> pathBuffer, ulong saveDataId)
    {
        ReadOnlySpan<byte> metaDirectoryName = new[]
        {
                (byte)'/', (byte)'s', (byte)'a', (byte)'v', (byte)'e', (byte)'M', (byte)'e', (byte)'t',
                (byte)'a', (byte)'/'
            };

        var sb = new U8StringBuilder(pathBuffer);
        sb.Append(metaDirectoryName).AppendFormat(saveDataId, 'x', 16);

        if (sb.Overflowed)
            return ResultFs.InvalidArgument.Log();

        return SetUpFixedPath(ref path, pathBuffer);
    }
}