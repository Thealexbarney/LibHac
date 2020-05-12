using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.FsSystem;

namespace LibHac.Fs
{
    public static class PathTool
    {
        // These are kept in nn::fs, but C# requires them to be inside a class
        internal const int EntryNameLengthMax = 0x300;
        internal const int MountNameLengthMax = 15;

        public static bool IsSeparator(byte c)
        {
            return c == StringTraits.DirectorySeparator;
        }

        public static bool IsAltSeparator(byte c)
        {
            return c == StringTraits.AltDirectorySeparator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsAnySeparator(byte c)
        {
            return IsSeparator(c) || IsAltSeparator(c);
        }

        public static bool IsNullTerminator(byte c)
        {
            return c == StringTraits.NullTerminator;
        }

        public static bool IsDot(byte c)
        {
            return c == StringTraits.Dot;
        }

        public static bool IsDriveSeparator(byte c)
        {
            return c == StringTraits.DriveSeparator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsCurrentDirectory(ReadOnlySpan<byte> p)
        {
            if ((uint)p.Length < 1) return false;

            ref byte b = ref MemoryMarshal.GetReference(p);

            return IsDot(b) && (p.Length == 1 || IsSeparator(Unsafe.Add(ref b, 1)) || IsNullTerminator(Unsafe.Add(ref b, 1)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsParentDirectory(ReadOnlySpan<byte> p)
        {
            if ((uint)p.Length < 2) return false;

            ref byte b = ref MemoryMarshal.GetReference(p);

            return IsDot(b) && IsDot(Unsafe.Add(ref b, 1)) &&
                   (p.Length == 2 || IsSeparator(Unsafe.Add(ref b, 2)) || IsNullTerminator(Unsafe.Add(ref b, 2)));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool IsParentDirectoryAlt(ReadOnlySpan<byte> p)
        {
            if ((uint)p.Length < 3) return false;

            ref byte b = ref MemoryMarshal.GetReference(p);

            return IsAnySeparator(b) && IsDot(Unsafe.Add(ref b, 1)) && IsDot(Unsafe.Add(ref b, 2)) &&
                   (p.Length == 3 || IsAnySeparator(Unsafe.Add(ref b, 3)) || IsNullTerminator(Unsafe.Add(ref b, 3)));
        }

        /// <summary>
        /// Checks if a path begins with / or \ and contains any of these patterns:
        /// "/..\", "\..\", "\../", "\..0" where '0' is the null terminator.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool ContainsParentDirectoryAlt(U8Span path2)
        {
            if (!IsAnySeparator(path2.GetOrNull(0))) return false;

            for (int i = 0; i < path2.Length - 2; i++)
            {
                byte c = path2[i];

                if (IsSeparator(c) &&
                    IsDot(path2[i + 1]) &&
                    IsDot(path2[i + 2]) &&
                    IsAltSeparator(path2.GetOrNull(i + 3)))
                {
                    return true;
                }

                if (IsAltSeparator(c) &&
                    IsDot(path2[i + 1]) &&
                    IsDot(path2[i + 2]))
                {
                    byte c3 = path2.GetOrNull(i + 3);

                    if (IsNullTerminator(c3) || IsAnySeparator(c3))
                    {
                        return true;
                    }
                }
                else if (IsNullTerminator(c))
                {
                    return false;
                }
            }

            return false;
        }

        public static Result Normalize(Span<byte> outputBuffer, out long normalizedLength, U8Span path,
            bool preserveUnc, bool hasMountName)
        {
            normalizedLength = default;

            U8Span path2 = path;
            int prefixLength = 0;
            bool isUncPath = false;

            if (hasMountName)
            {
                Result rc = ParseMountName(out path2, outputBuffer, out long mountNameLength, path2);
                if (rc.IsFailure()) return rc;

                prefixLength += (int)mountNameLength;
            }

            if (preserveUnc)
            {
                U8Span originalPath = path2;

                Result rc = ParseWindowsPath(out path2, outputBuffer.Slice(prefixLength), out long windowsPathLength,
                    out _, false, path2, hasMountName);
                if (rc.IsFailure()) return rc;

                prefixLength += (int)windowsPathLength;
                if (originalPath.Value != path2.Value)
                {
                    isUncPath = true;
                }
            }

            if (prefixLength == 0 && !IsSeparator(path2.GetOrNull(0)))
                return ResultFs.InvalidPathFormat.Log();

            if (ContainsParentDirectoryAlt(path2))
            {
                var buffer2 = new byte[PathTools.MaxPathLength + 1];

                buffer2[0] = StringTraits.DirectorySeparator;
                int j;

                for (j = 1; j < path2.Length; j++)
                {
                    byte c = path2[j];
                    if (IsNullTerminator(c))
                        break;

                    // Current char is a dot. Check the surrounding chars for the /../ pattern
                    if (IsDot(c) && IsParentDirectoryAlt(path2.Value.Slice(j - 1)))
                    {
                        buffer2[j - 1] = StringTraits.DirectorySeparator;
                        buffer2[j] = StringTraits.Dot;
                        buffer2[j + 1] = StringTraits.Dot;

                        j += 2;

                        if (!IsNullTerminator(path2.GetOrNull(j)))
                        {
                            buffer2[j] = StringTraits.DirectorySeparator;
                        }
                    }
                    else
                    {
                        buffer2[j] = c;
                    }
                }

                buffer2[j] = StringTraits.NullTerminator;
                path2 = new U8Span(buffer2);
            }

            int i = 0;
            bool skipNextSep = false;
            int totalLength = prefixLength;

            while (i < path2.Length && !IsNullTerminator(path2[i]))
            {
                if (IsSeparator(path2[i]))
                {
                    do
                    {
                        i++;
                    } while (i < path2.Length && IsSeparator(path2[i]));

                    if (i >= path2.Length || IsNullTerminator(path2[i]))
                    {
                        break;
                    }

                    if (!skipNextSep)
                    {
                        if (totalLength + 1 == outputBuffer.Length)
                        {
                            outputBuffer[totalLength] = StringTraits.NullTerminator;
                            normalizedLength = totalLength;

                            return ResultFs.TooLongPath.Log();
                        }

                        outputBuffer[totalLength++] = StringTraits.DirectorySeparator;
                    }

                    skipNextSep = false;
                }

                int dirLen = 0;
                while (path2.Length > i + dirLen && !IsNullTerminator(path2[i + dirLen]) && !IsSeparator(path2[i + dirLen]))
                {
                    dirLen++;
                }

                if (IsCurrentDirectory(path2.Slice(i)))
                {
                    skipNextSep = true;
                }
                else if (IsParentDirectory(path2.Slice(i)))
                {
                    Debug.Assert(IsSeparator(outputBuffer[totalLength - 1]));
                    Debug.Assert(IsSeparator(outputBuffer[prefixLength]));

                    if (totalLength == prefixLength + 1)
                    {
                        if (isUncPath)
                        {
                            totalLength--;
                        }
                        else
                        {
                            return ResultFs.DirectoryUnobtainable.Log();
                        }
                    }
                    else
                    {
                        totalLength -= 2;
                        while (!IsSeparator(outputBuffer[totalLength]))
                        {
                            totalLength--;

                            if (totalLength == prefixLength)
                            {
                                break;
                            }
                        }
                    }

                    Debug.Assert(IsSeparator(outputBuffer[totalLength]));
                    Debug.Assert(totalLength < outputBuffer.Length);
                }
                else
                {
                    if (totalLength + dirLen + 1 <= outputBuffer.Length)
                    {
                        for (int j = 0; j < dirLen; j++)
                        {
                            outputBuffer[totalLength++] = path2[i + j];
                        }
                    }
                    else
                    {
                        int copyLen = outputBuffer.Length - 1 - totalLength;

                        for (int j = 0; j < copyLen; j++)
                        {
                            outputBuffer[totalLength++] = path2[i + j];
                        }

                        outputBuffer[totalLength] = StringTraits.NullTerminator;
                        normalizedLength = totalLength;
                        return ResultFs.TooLongPath.Log();
                    }
                }

                i += dirLen;
            }

            if (skipNextSep)
                totalLength--;

            if (totalLength < outputBuffer.Length && totalLength == prefixLength && !isUncPath)
            {
                outputBuffer[prefixLength] = StringTraits.DirectorySeparator;
                totalLength++;
            }

            if (totalLength - 1 > outputBuffer.Length)
            {
                return ResultFs.TooLongPath.Log();
            }

            outputBuffer[totalLength] = StringTraits.NullTerminator;

            Debug.Assert(IsNormalized(out bool isNormalized, new U8Span(outputBuffer), preserveUnc, hasMountName).IsSuccess());
            Debug.Assert(isNormalized);

            normalizedLength = totalLength;
            return Result.Success;
        }

        public static Result IsNormalized(out bool isNormalized, U8Span path, bool preserveUnc, bool hasMountName)
        {
            isNormalized = default;
            U8Span path2 = path;
            bool isUncPath = false;

            if (hasMountName)
            {
                Result rc = ParseMountName(out path2, Span<byte>.Empty, out _, path);
                if (rc.IsFailure()) return rc;

                if (path2.Length == 0 || !IsSeparator(path2[0]))
                    return ResultFs.InvalidPathFormat.Log();
            }

            if (preserveUnc)
            {
                U8Span originalPath = path2;

                Result rc = ParseWindowsPath(out path2, Span<byte>.Empty, out _, out bool isNormalizedWin,
                    true, originalPath, hasMountName);
                if (rc.IsFailure()) return rc;

                if (!isNormalizedWin)
                {
                    isNormalized = false;
                    return Result.Success;
                }

                // Path is a UNC path if the new path skips part of the original
                isUncPath = originalPath.Value != path2.Value;

                if (isUncPath)
                {
                    if (IsSeparator(originalPath.GetOrNull(0)) && IsSeparator(originalPath.GetOrNull(1)))
                    {
                        isNormalized = false;
                        return Result.Success;
                    }

                    if (IsNullTerminator(path2.GetOrNull(0)))
                    {
                        isNormalized = true;
                        return Result.Success;
                    }
                }
            }

            if (path2.IsEmpty())
                return ResultFs.InvalidPathFormat.Log();

            if (ContainsParentDirectoryAlt(path2))
            {
                isNormalized = false;
                return Result.Success;
            }

            bool pathWasSkipped = path.Value != path2.Value;
            var state = NormalizeState.Initial;

            for (int i = 0; i < path2.Length; i++)
            {
                byte c = path2[i];
                if (IsNullTerminator(c)) break;

                switch (state)
                {
                    // I don't think this first case can actually be triggered, but Nintendo has it there anyway
                    case NormalizeState.Initial when pathWasSkipped && IsDot(c): state = NormalizeState.Dot; break;
                    case NormalizeState.Initial when IsSeparator(c): state = NormalizeState.FirstSeparator; break;
                    case NormalizeState.Initial when !pathWasSkipped: return ResultFs.InvalidPathFormat.Log();
                    case NormalizeState.Initial: state = NormalizeState.Normal; break;

                    case NormalizeState.Normal when IsSeparator(c): state = NormalizeState.Separator; break;

                    case NormalizeState.FirstSeparator when IsDot(c):
                    case NormalizeState.Separator when IsDot(c):
                        state = NormalizeState.Dot;
                        break;

                    case NormalizeState.FirstSeparator when IsSeparator(c):
                    case NormalizeState.Separator when IsSeparator(c):
                        isNormalized = false;
                        return Result.Success;

                    case NormalizeState.FirstSeparator:
                    case NormalizeState.Separator:
                        state = NormalizeState.Normal;
                        break;

                    case NormalizeState.Dot when IsSeparator(c):
                        isNormalized = false;
                        return Result.Success;

                    case NormalizeState.Dot when IsDot(c): state = NormalizeState.DoubleDot; break;
                    case NormalizeState.Dot: state = NormalizeState.Normal; break;

                    case NormalizeState.DoubleDot when IsSeparator(c):
                        isNormalized = false;
                        return Result.Success;

                    case NormalizeState.DoubleDot: state = NormalizeState.Normal; break;
                }
            }

            switch (state)
            {
                case NormalizeState.Initial:
                    return ResultFs.InvalidPathFormat.Log();
                case NormalizeState.Normal:
                    isNormalized = true;
                    break;
                case NormalizeState.FirstSeparator:
                    isNormalized = !isUncPath;
                    break;
                case NormalizeState.Separator:
                case NormalizeState.Dot:
                case NormalizeState.DoubleDot:
                    isNormalized = false;
                    break;
            }

            return Result.Success;
        }

        public static bool IsSubpath(U8Span lhs, U8Span rhs)
        {
            bool isUncLhs = PathUtility.IsUnc(lhs);
            bool isUncRhs = PathUtility.IsUnc(rhs);

            if (isUncLhs && !isUncRhs || !isUncLhs && isUncRhs)
                return false;

            if (IsSeparator(lhs.GetOrNull(0)) && IsNullTerminator(lhs.GetOrNull(1)) &&
                IsSeparator(rhs.GetOrNull(0)) && !IsNullTerminator(rhs.GetOrNull(1)))
                return true;

            if (IsSeparator(rhs.GetOrNull(0)) && IsNullTerminator(rhs.GetOrNull(1)) &&
                IsSeparator(lhs.GetOrNull(0)) && !IsNullTerminator(lhs.GetOrNull(1)))
                return true;

            for (int i = 0; ; i++)
            {
                if (IsNullTerminator(lhs.GetOrNull(i)))
                {
                    return IsSeparator(rhs.GetOrNull(i));
                }
                else if (IsNullTerminator(rhs.GetOrNull(i)))
                {
                    return IsSeparator(lhs.GetOrNull(i));
                }
                else if (lhs.GetOrNull(i) != rhs.GetOrNull(i))
                {
                    return false;
                }
            }
        }

        private static Result ParseMountName(out U8Span pathAfterMount, Span<byte> outMountNameBuffer,
            out long mountNameLength, U8Span path)
        {
            pathAfterMount = default;
            mountNameLength = default;

            int mountStart = IsSeparator(path.GetOrNull(0)) ? 1 : 0;
            int mountEnd;

            int maxMountLength = Math.Min(PathTools.MountNameLengthMax, path.Length - mountStart);

            for (mountEnd = mountStart; mountEnd <= maxMountLength; mountEnd++)
            {
                byte c = path[mountEnd];

                if (IsSeparator(c))
                {
                    pathAfterMount = path;
                    mountNameLength = 0;

                    return Result.Success;
                }

                if (IsDriveSeparator(c))
                {
                    mountEnd++;
                    break;
                }

                if (IsNullTerminator(c))
                {
                    break;
                }
            }

            if (mountStart >= mountEnd - 1 || !IsDriveSeparator(path[mountEnd - 1]))
                return ResultFs.InvalidPathFormat.Log();

            for (int i = mountStart; i < mountEnd; i++)
            {
                if (IsDot(path[i]))
                    return ResultFs.InvalidCharacter.Log();
            }

            if (!outMountNameBuffer.IsEmpty)
            {
                if (mountEnd - mountStart > outMountNameBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                path.Value.Slice(0, mountEnd).CopyTo(outMountNameBuffer);
            }

            pathAfterMount = path.Slice(mountEnd);
            mountNameLength = mountEnd - mountStart;
            return Result.Success;
        }

        private static Result ParseWindowsPath(out U8Span newPath, Span<byte> buffer, out long windowsPathLength,
            out bool isNormalized, bool checkIfNormalized, U8Span path, bool hasMountName)
        {
            newPath = default;
            windowsPathLength = 0;
            isNormalized = checkIfNormalized;

            int winPathLen = 0;
            int mountNameLen = 0;
            bool skippedMount = false;

            bool needsSeparatorFixup = false;

            if (hasMountName)
            {
                if (IsSeparator(path.GetOrNull(0)) && IsAltSeparator(path.GetOrNull(1)) &&
                    IsAltSeparator(path.GetOrNull(2)))
                {
                    mountNameLen = 1;
                    skippedMount = true;
                }
                else
                {
                    int separatorCount = 0;

                    while (IsSeparator(path.GetOrNull(separatorCount)))
                    {
                        separatorCount++;
                    }

                    if (separatorCount == 1 || PathUtility.IsWindowsDrive(path.Slice(separatorCount)))
                    {
                        mountNameLen = separatorCount;
                        skippedMount = true;
                    }
                    else if (separatorCount > 2 && checkIfNormalized)
                    {
                        isNormalized = false;
                        return Result.Success;
                    }
                    else if (separatorCount > 1)
                    {
                        mountNameLen = separatorCount - 2;
                        skippedMount = true;
                    }
                }
            }
            else
            {
                if (IsSeparator(path.GetOrNull(0)) && !PathUtility.IsUnc(path))
                {
                    mountNameLen = 1;
                    skippedMount = true;
                }
            }

            U8Span pathTrimmed = path.Slice(mountNameLen);

            if (PathUtility.IsWindowsDrive(pathTrimmed))
            {
                int i = 2;

                while (!IsAnySeparator(pathTrimmed.GetOrNull(i)) && !IsNullTerminator(pathTrimmed.GetOrNull(i)))
                {
                    i++;
                }

                winPathLen = mountNameLen + i;

                if (!buffer.IsEmpty)
                {
                    if (winPathLen > buffer.Length)
                    {
                        return ResultFs.TooLongPath.Log();
                    }

                    path.Value.Slice(0, winPathLen).CopyTo(buffer);
                }

                newPath = path.Slice(winPathLen);
                windowsPathLength = winPathLen;
                return Result.Success;
            }

            // A UNC path should be in the format "\\" host-name "\" share-name [ "\" object-name ]
            if (PathUtility.IsUnc(pathTrimmed))
            {
                if (IsAnySeparator(pathTrimmed.GetOrNull(2)))
                {
                    return ResultFs.InvalidPathFormat.Log();
                }

                int currentComponentStart = 2;

                for (int i = 2; ; i++)
                {
                    byte c = pathTrimmed.GetOrNull(i);

                    if (IsAnySeparator(c))
                    {
                        // Nintendo feels the need to change the '\' separators to '/'s
                        if (IsAltSeparator(c))
                        {
                            needsSeparatorFixup = true;

                            if (checkIfNormalized)
                            {
                                isNormalized = false;
                                return Result.Success;
                            }
                        }

                        // make sure share-name is not empty
                        if (currentComponentStart == 2 && IsSeparator(pathTrimmed.GetOrNull(i + 1)))
                        {
                            return ResultFs.InvalidPathFormat.Log();
                        }

                        int componentLength = i - currentComponentStart;

                        // neither host-name nor share-name can be "." or ".."
                        if (componentLength == 1 && IsDot(pathTrimmed[currentComponentStart]))
                        {
                            return ResultFs.InvalidPathFormat.Log();
                        }

                        if (componentLength == 2 && IsDot(pathTrimmed[currentComponentStart]) &&
                            IsDot(pathTrimmed[currentComponentStart + 1]))
                        {
                            return ResultFs.InvalidPathFormat.Log();
                        }

                        // If we're currently processing the share-name path component
                        if (currentComponentStart != 2)
                        {
                            winPathLen = mountNameLen + i;
                            break;
                        }

                        currentComponentStart = i + 1;
                    }
                    else if (c == (byte)'$' || IsDriveSeparator(c))
                    {
                        // '$' and ':' are not allowed in the host-name path component
                        if (currentComponentStart == 2)
                        {
                            return ResultFs.InvalidCharacter.Log();
                        }

                        // A '$' or ':' must be the last character in share-name
                        byte nextChar = pathTrimmed.GetOrNull(i + 1);
                        if (!IsSeparator(nextChar) && !IsNullTerminator(nextChar))
                        {
                            return ResultFs.InvalidPathFormat.Log();
                        }

                        winPathLen = mountNameLen + i + 1;
                        break;
                    }
                    else if (IsNullTerminator(c))
                    {
                        if (currentComponentStart != 2)
                        {
                            int componentLength = i - currentComponentStart;

                            // neither host-name nor share-name can be "." or ".."
                            if (componentLength == 1 && IsDot(pathTrimmed[currentComponentStart]))
                            {
                                return ResultFs.InvalidPathFormat.Log();
                            }

                            if (componentLength == 2 && IsDot(pathTrimmed[currentComponentStart]) &&
                                IsDot(pathTrimmed[currentComponentStart + 1]))
                            {
                                return ResultFs.InvalidPathFormat.Log();
                            }

                            winPathLen = mountNameLen + i;
                        }

                        break;
                    }
                }

                if (!buffer.IsEmpty)
                {
                    if (winPathLen - mountNameLen > buffer.Length)
                    {
                        return ResultFs.TooLongPath.Log();
                    }

                    int outPos = 0;

                    if (skippedMount)
                    {
                        buffer[0] = StringTraits.DirectorySeparator;
                        outPos++;
                    }

                    pathTrimmed.Value.Slice(0, winPathLen - mountNameLen).CopyTo(buffer.Slice(outPos));

                    buffer[outPos] = StringTraits.AltDirectorySeparator;
                    buffer[outPos + 1] = StringTraits.AltDirectorySeparator;

                    if (needsSeparatorFixup)
                    {
                        for (int i = mountNameLen + 2; i < winPathLen; i++)
                        {
                            if (IsAltSeparator(buffer[i]))
                            {
                                buffer[i] = StringTraits.DirectorySeparator;
                            }
                        }
                    }

                    newPath = path.Slice(winPathLen);
                    windowsPathLength = outPos + winPathLen - mountNameLen;
                    return Result.Success;
                }
            }
            newPath = path.Slice(winPathLen);
            return Result.Success;
        }

        private enum NormalizeState
        {
            Initial,
            Normal,
            FirstSeparator,
            Separator,
            Dot,
            DoubleDot
        }
    }
}
