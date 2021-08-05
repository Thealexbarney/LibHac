using System;
using System.Buffers;
using System.Diagnostics;
using LibHac.Common;
using LibHac.Diag;
using LibHac.Util;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    public struct PathFlags
    {
        private uint _value;

        public void AllowWindowsPath() => _value |= 1 << 0;
        public void AllowRelativePath() => _value |= 1 << 1;
        public void AllowEmptyPath() => _value |= 1 << 2;
        public void AllowMountName() => _value |= 1 << 3;
        public void AllowBackslash() => _value |= 1 << 4;

        public bool IsWindowsPathAllowed() => (_value & (1 << 0)) != 0;
        public bool IsRelativePathAllowed() => (_value & (1 << 1)) != 0;
        public bool IsEmptyPathAllowed() => (_value & (1 << 2)) != 0;
        public bool IsMountNameAllowed() => (_value & (1 << 3)) != 0;
        public bool IsBackslashAllowed() => (_value & (1 << 4)) != 0;
    }

    [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
    public ref struct Path
    {
        [DebuggerDisplay("{" + nameof(ToString) + "(),nq}")]
        public struct Stored : IDisposable
        {
            private byte[] _buffer;
            private int _length;

            public void Dispose()
            {
                byte[] buffer = Shared.Move(ref _buffer);
                if (buffer is not null)
                {
                    ArrayPool<byte>.Shared.Return(buffer);
                }
            }

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
            public readonly Path GetPath()
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

                if (oldBuffer is not null)
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

        // Todo: Hack around "using" variables being read only
        public void Dispose()
        {
            byte[] writeBuffer = Shared.Move(ref _writeBuffer);
            if (writeBuffer is not null)
            {
                ArrayPool<byte>.Shared.Return(writeBuffer);
            }
        }

        internal Span<byte> GetWriteBuffer()
        {
            Assert.SdkRequires(_writeBuffer is not null);
            return _writeBuffer.AsSpan();
        }

        internal readonly long GetWriteBufferLength()
        {
            return _writeBufferLength;
        }

        public readonly int GetLength()
        {
            return StringUtils.GetLength(GetString());
        }

        public readonly bool IsEmpty()
        {
            return _string.At(0) == 0;
        }

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

        public byte[] ReleaseBuffer()
        {
            Assert.SdkRequires(_writeBuffer is not null);

            _string = EmptyPath;
            _writeBufferLength = 0;

            return Shared.Move(ref _writeBuffer);
        }

        private void ClearBuffer()
        {
            byte[] oldBuffer = Shared.Move(ref _writeBuffer);

            if (oldBuffer is not null)
                ArrayPool<byte>.Shared.Return(oldBuffer);

            _writeBufferLength = 0;
            _string = EmptyPath;
        }

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

        private void SetReadOnlyBuffer(ReadOnlySpan<byte> buffer)
        {
            _string = buffer;

            byte[] oldBuffer = Shared.Move(ref _writeBuffer);

            if (oldBuffer is not null)
                ArrayPool<byte>.Shared.Return(oldBuffer);

            _writeBufferLength = 0;
        }

        private Result Preallocate(int length)
        {
            if (_writeBufferLength > length)
                return Result.Success;

            int alignedLength = Alignment.AlignUpPow2(length, WriteBufferAlignmentLength);
            byte[] buffer = ArrayPool<byte>.Shared.Rent(alignedLength);
            SetModifiableBuffer(buffer, alignedLength);

            return Result.Success;
        }

        public Result SetShallowBuffer(ReadOnlySpan<byte> buffer)
        {
            Assert.SdkRequires(_writeBufferLength == 0);

            SetReadOnlyBuffer(buffer);
            _isNormalized = true;
            return Result.Success;
        }

        public readonly ReadOnlySpan<byte> GetString()
        {
            Assert.SdkAssert(_isNormalized);

            return _string;
        }

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

        public Result Initialize(ReadOnlySpan<byte> path)
        {
            Result rc = InitializeImpl(path, StringUtils.GetLength(path));
            if (rc.IsFailure()) return rc;

            _isNormalized = false;
            return Result.Success;
        }

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

        public Result Initialize(ReadOnlySpan<byte> path, int length)
        {
            Result rc = InitializeImpl(path, length);
            if (rc.IsFailure()) return rc;

            _isNormalized = false;
            return Result.Success;
        }

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

        public Result InitializeAsEmpty()
        {
            ClearBuffer();
            _isNormalized = true;

            return Result.Success;
        }

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

        public Result InsertParent(in Path parent)
        {
            return InsertParent(parent.GetString());
        }

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

        public Result AppendChild(in Path child)
        {
            return AppendChild(child.GetString());
        }

        public Result Combine(in Path path1, in Path path2)
        {
            int path1Length = path1.GetLength();
            int path2Length = path2.GetLength();

            Result rc = Preallocate(path1Length + SeparatorLength + path2Length + NullTerminatorLength);
            if (rc.IsFailure()) return rc;

            rc = Initialize(path1);
            if (rc.IsFailure()) return rc;

            if (IsEmpty())
            {
                rc = Initialize(path2);
                if (rc.IsFailure()) return rc;
            }
            else
            {
                rc = AppendChild(path2);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }

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

        public override string ToString() => StringUtils.Utf8ZToString(_string);

        public override bool Equals(object obj) => throw new NotSupportedException();
        public override int GetHashCode() => throw new NotImplementedException();
    }

    public static class PathFunctions
    {
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
        internal static Result SetUpFixedPathSingleEntry(ref Path path, Span<byte> pathBuffer,
            ReadOnlySpan<byte> entryName)
        {
            var sb = new U8StringBuilder(pathBuffer);
            sb.Append((byte)'/').Append(entryName);

            if (sb.Overflowed)
                return ResultFs.InvalidArgument.Log();

            return SetUpFixedPath(ref path, pathBuffer);
        }

        // /%016llx
        internal static Result SetUpFixedPathSaveId(ref Path path, Span<byte> pathBuffer, ulong saveDataId)
        {
            var sb = new U8StringBuilder(pathBuffer);
            sb.Append((byte)'/').AppendFormat(saveDataId, 'x', 16);

            if (sb.Overflowed)
                return ResultFs.InvalidArgument.Log();

            return SetUpFixedPath(ref path, pathBuffer);
        }

        // /%08x.meta
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
}
