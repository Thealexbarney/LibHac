using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Diag;
using LibHac.FsSystem;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    // Previous normalization code can be found in commit 1acdd86e27de16703fdb1c77f50ed8fd71bd3ad7
    public static class PathNormalizer
    {
        private enum NormalizeState
        {
            Initial,
            Normal,
            FirstSeparator,
            Separator,
            Dot,
            DoubleDot
        }

        /// <summary>
        /// Checks if a host-name or share-name in a UNC path are "." or ".."
        /// </summary>
        /// <param name="path">Up to the first two characters in a segment of a UNC path.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.BufferAllocationFailed"/>: A buffer could not be allocated.</returns>
        private static Result CheckSharedName(U8Span path)
        {
            if (path.Length == 1 && path.GetUnsafe(0) == Dot)
                return ResultFs.InvalidPathFormat.Log();

            if (path.Length == 2 && path.GetUnsafe(0) == Dot && path.GetUnsafe(1) == Dot)
                return ResultFs.InvalidPathFormat.Log();

            return Result.Success;
        }

        private static Result ParseWindowsPath(out U8Span newPath, Span<byte> buffer, out long windowsPathLength,
            out bool isUncNormalized, U8Span path, bool hasMountName)
        {
            UnsafeHelpers.SkipParamInit(out windowsPathLength, out isUncNormalized);
            newPath = default;

            U8Span currentPath = path;

            if (!Unsafe.IsNullRef(ref isUncNormalized))
                isUncNormalized = true;

            bool skippedMount = false;
            int prefixLength = 0;

            if (hasMountName)
            {
                if (path.GetOrNull(0) == DirectorySeparator && path.GetOrNull(1) == AltDirectorySeparator &&
                    path.GetOrNull(1) == AltDirectorySeparator)
                {
                    currentPath = currentPath.Slice(1);
                    skippedMount = true;
                }
                else
                {
                    int separatorCount = 0;

                    while (IsSeparator(currentPath.GetOrNull(separatorCount)))
                    {
                        separatorCount++;
                    }

                    if (separatorCount != 0)
                    {
                        if (separatorCount == 1 || WindowsPath.IsWindowsDrive(currentPath.Slice(separatorCount)))
                        {
                            currentPath = currentPath.Slice(separatorCount);
                            skippedMount = true;
                        }
                        else
                        {
                            if (separatorCount > 2 && !Unsafe.IsNullRef(ref isUncNormalized))
                            {
                                isUncNormalized = false;
                                return Result.Success;
                            }

                            currentPath = currentPath.Slice(separatorCount - 2);
                            prefixLength = 1;
                        }
                    }
                }
            }
            else if (path.GetOrNull(0) == DirectorySeparator && !WindowsPath.IsUnc(path))
            {
                currentPath = currentPath.Slice(1);
                skippedMount = true;
            }

            U8Span trimmedPath = path;

            if (WindowsPath.IsWindowsDrive(currentPath))
            {
                int i;
                for (i = 2; currentPath.GetOrNull(i) != NullTerminator; i++)
                {
                    if (currentPath[i] == DirectorySeparator || currentPath[i] == AltDirectorySeparator)
                    {
                        trimmedPath = currentPath.Slice(i);
                        break;
                    }
                }

                if (trimmedPath.Value == path.Value)
                    trimmedPath = currentPath.Slice(i);

                ref byte pathStart = ref MemoryMarshal.GetReference(path.Value);
                ref byte trimmedPathStart = ref MemoryMarshal.GetReference(trimmedPath.Value);
                int winPathLength = (int)Unsafe.ByteOffset(ref pathStart, ref trimmedPathStart);

                if (!buffer.IsEmpty)
                {
                    if (winPathLength > buffer.Length)
                        return ResultFs.TooLongPath.Log();

                    path.Value.Slice(0, winPathLength).CopyTo(buffer);
                }

                newPath = trimmedPath;
                windowsPathLength = winPathLength;
                return Result.Success;
            }
            // A UNC path should be in the format "\\" host-name "\" share-name [ "\" object-name ]
            else if (WindowsPath.IsUnc(currentPath))
            {
                if (currentPath.GetOrNull(2) == DirectorySeparator || currentPath.GetOrNull(2) == AltDirectorySeparator)
                {
                    Assert.SdkAssert(!hasMountName);
                    return ResultFs.InvalidPathFormat.Log();
                }
                else
                {
                    bool needsSeparatorFix = false;
                    int currentComponentOffset = 0;

                    for (int i = 2; currentPath.GetOrNull(i) != NullTerminator; i++)
                    {
                        byte c = currentPath.GetUnsafe(i);

                        // Check if we need to fix the separators
                        if (currentComponentOffset == 0 && c == AltDirectorySeparator)
                        {
                            needsSeparatorFix = true;

                            if (!Unsafe.IsNullRef(ref isUncNormalized))
                            {
                                isUncNormalized = false;
                                return Result.Success;
                            }
                        }

                        if (c == DirectorySeparator || c == AltDirectorySeparator)
                        {
                            if (c == AltDirectorySeparator)
                                needsSeparatorFix = true;

                            if (currentComponentOffset != 0)
                                break;

                            if (IsSeparator(currentPath.GetOrNull(i + 1)))
                                return ResultFs.InvalidPathFormat.Log();

                            Result rc = CheckSharedName(currentPath.Slice(2, i - 2));
                            if (rc.IsFailure()) return rc;

                            currentComponentOffset = i + 1;
                        }

                        if (c == (byte)'$' || c == DriveSeparator)
                        {
                            if (currentComponentOffset == 0)
                                return ResultFs.InvalidCharacter.Log();

                            // A '$' or ':' must be the last character in share-name
                            byte nextChar = currentPath.GetOrNull(i + 1);
                            if (nextChar != DirectorySeparator && nextChar != AltDirectorySeparator &&
                                nextChar != NullTerminator)
                            {
                                return ResultFs.InvalidPathFormat.Log();
                            }

                            trimmedPath = currentPath.Slice(i + 1);
                            break;
                        }
                    }

                    if (trimmedPath.Value == path.Value)
                    {
                        int trimmedPartOffset = 0;

                        int i;
                        for (i = 2; currentPath.GetOrNull(i) != NullTerminator; i++)
                        {
                            byte c = currentPath.GetUnsafe(i);

                            if (c == DirectorySeparator || c == AltDirectorySeparator)
                            {
                                Result rc;

                                if (trimmedPartOffset != 0)
                                {
                                    rc = CheckSharedName(currentPath.Slice(trimmedPartOffset, i - trimmedPartOffset));
                                    if (rc.IsFailure()) return rc;

                                    trimmedPath = currentPath.Slice(i);
                                    break;
                                }

                                if (IsSeparator(currentPath.GetOrNull(i + 1)))
                                {
                                    return ResultFs.InvalidPathFormat.Log();
                                }

                                rc = CheckSharedName(currentPath.Slice(2, i - 2));
                                if (rc.IsFailure()) return rc;

                                trimmedPartOffset = i + 1;
                            }
                        }

                        if (trimmedPartOffset != 0 && trimmedPath.Value == path.Value)
                        {
                            Result rc = CheckSharedName(currentPath.Slice(trimmedPartOffset, i - trimmedPartOffset));
                            if (rc.IsFailure()) return rc;

                            trimmedPath = currentPath.Slice(i);
                        }
                    }

                    ref byte trimmedPathStart = ref MemoryMarshal.GetReference(trimmedPath.Value);
                    ref byte currentPathStart = ref MemoryMarshal.GetReference(currentPath.Value);
                    int mountLength = (int)Unsafe.ByteOffset(ref currentPathStart, ref trimmedPathStart);
                    bool prependSeparator = prefixLength != 0 || skippedMount;

                    if (!buffer.IsEmpty)
                    {
                        if (mountLength > buffer.Length)
                        {
                            return ResultFs.TooLongPath.Log();
                        }

                        Span<byte> currentBuffer = buffer;
                        if (prependSeparator)
                        {
                            currentBuffer[0] = DirectorySeparator;
                            currentBuffer = currentBuffer.Slice(1);
                        }

                        currentPath.Value.Slice(0, mountLength).CopyTo(currentBuffer);
                        currentBuffer[0] = AltDirectorySeparator;
                        currentBuffer[1] = AltDirectorySeparator;

                        if (needsSeparatorFix)
                        {
                            for (int i = 2; i < mountLength; i++)
                            {
                                if (currentBuffer[i] == AltDirectorySeparator)
                                    currentBuffer[i] = DirectorySeparator;
                            }
                        }
                    }

                    newPath = trimmedPath;
                    windowsPathLength = mountLength + (prependSeparator ? 1 : 0);
                    return Result.Success;
                }
            }
            else
            {
                newPath = trimmedPath;
                return Result.Success;
            }
        }

        private static Result SkipWindowsPath(out U8Span newPath, out bool isUncNormalized, U8Span path,
            bool hasMountName)
        {
            return ParseWindowsPath(out newPath, Span<byte>.Empty, out _, out isUncNormalized, path, hasMountName);
        }

        private static Result ParseMountName(out U8Span newPath, Span<byte> outMountNameBuffer,
            out long mountNameLength, U8Span path)
        {
            UnsafeHelpers.SkipParamInit(out mountNameLength);
            newPath = default;

            int mountStart = IsSeparator(path.GetOrNull(0)) ? 1 : 0;
            int mountEnd;

            int maxMountLength = Math.Min(PathTools.MountNameLengthMax, path.Length - mountStart);

            for (mountEnd = mountStart; mountEnd <= maxMountLength; mountEnd++)
            {
                byte c = path[mountEnd];

                if (IsSeparator(c))
                {
                    newPath = path;
                    mountNameLength = 0;

                    return Result.Success;
                }

                if (c == DriveSeparator)
                {
                    mountEnd++;
                    break;
                }

                if (c == NullTerminator)
                {
                    break;
                }
            }

            if (mountStart >= mountEnd - 1 || path[mountEnd - 1] != DriveSeparator)
                return ResultFs.InvalidPathFormat.Log();

            if (mountEnd != mountStart)
            {
                for (int i = mountStart; i < mountEnd; i++)
                {
                    if (path[i] == Dot)
                        return ResultFs.InvalidCharacter.Log();
                }
            }

            if (!outMountNameBuffer.IsEmpty)
            {
                if (mountEnd - mountStart > outMountNameBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                path.Value.Slice(0, mountEnd).CopyTo(outMountNameBuffer);
            }

            newPath = path.Slice(mountEnd);
            mountNameLength = mountEnd - mountStart;
            return Result.Success;
        }

        private static Result SkipMountName(out U8Span newPath, U8Span path)
        {
            return ParseMountName(out newPath, Span<byte>.Empty, out _, path);
        }

        /// <summary>
        /// Checks if a path begins with / or \ and contains any of these patterns:
        /// "/..\", "\..\", "\../", "\..0" where '0' is the null terminator.
        /// </summary>
        private static bool IsParentDirectoryPathReplacementNeeded(U8Span path)
        {
            if (!IsAnySeparator(path.GetOrNull(0)))
                return false;

            for (int i = 0; i < path.Length - 2 && path[i] != NullTerminator; i++)
            {
                byte c3 = path.GetOrNull(i + 3);

                if (path[i] == AltDirectorySeparator &&
                    path[i + 1] == Dot &&
                    path[i + 2] == Dot &&
                    (IsAnySeparator(c3) || c3 == NullTerminator))
                {
                    return true;
                }

                if (IsAnySeparator(path[i]) &&
                    path[i + 1] == Dot &&
                    path[i + 2] == Dot &&
                    c3 == AltDirectorySeparator)
                {
                    return true;
                }
            }

            return false;
        }

        private static void ReplaceParentDirectoryPath(Span<byte> dest, ReadOnlySpan<byte> source)
        {
            dest[0] = DirectorySeparator;

            int i = 1;
            while (source.Length > i && source[i] != NullTerminator)
            {
                if (source.Length > i + 2 &&
                    IsAnySeparator(source[i - 1]) &&
                    source[i + 0] == Dot &&
                    source[i + 1] == Dot &&
                    IsAnySeparator(source[i + 2]))
                {
                    dest[i - 1] = DirectorySeparator;
                    dest[i + 0] = Dot;
                    dest[i + 1] = Dot;
                    dest[i + 2] = DirectorySeparator;
                    i += 3;
                }
                else
                {
                    if (source.Length > i + 1 &&
                        source[i - 1] == AltDirectorySeparator &&
                        source[i + 0] == Dot &&
                        source[i + 1] == Dot &&
                        (source.Length == i + 2 || source[i + 2] == NullTerminator))
                    {
                        dest[i - 1] = DirectorySeparator;
                        dest[i + 0] = Dot;
                        dest[i + 1] = Dot;
                        i += 2;
                        break;
                    }

                    dest[i] = source[i];
                    i++;
                }
            }

            dest[i] = NullTerminator;
        }

        public static Result Normalize(Span<byte> outputBuffer, out long normalizedLength, U8Span path,
            bool preserveUnc, bool hasMountName)
        {
            UnsafeHelpers.SkipParamInit(out normalizedLength);

            U8Span currentPath = path;
            int prefixLength = 0;
            bool isUncPath = false;

            if (hasMountName)
            {
                Result rc = ParseMountName(out currentPath, outputBuffer, out long mountNameLength, currentPath);
                if (rc.IsFailure()) return rc;

                prefixLength += (int)mountNameLength;
            }

            if (preserveUnc)
            {
                U8Span originalPath = currentPath;

                Result rc = ParseWindowsPath(out currentPath, outputBuffer.Slice(prefixLength),
                    out long windowsPathLength, out Unsafe.NullRef<bool>(), currentPath, hasMountName);
                if (rc.IsFailure()) return rc;

                prefixLength += (int)windowsPathLength;
                if (originalPath.Value != currentPath.Value)
                {
                    isUncPath = true;
                }
            }

            // Paths must start with /
            if (prefixLength == 0 && !IsSeparator(currentPath.GetOrNull(0)))
                return ResultFs.InvalidPathFormat.Log();

            var convertedPath = new RentedArray<byte>();
            try
            {
                // Check if parent directory path replacement is needed.
                if (IsParentDirectoryPathReplacementNeeded(currentPath))
                {
                    // Allocate a buffer to hold the replacement path.
                    convertedPath = new RentedArray<byte>(PathTools.MaxPathLength + 1);

                    // Replace the path.
                    ReplaceParentDirectoryPath(convertedPath.Span, currentPath);

                    // Set current path to be the replacement path.
                    currentPath = new U8Span(convertedPath.Span);
                }

                bool skipNextSep = false;
                int i = 0;
                int totalLength = prefixLength;

                while (!IsNul(currentPath.GetOrNull(i)))
                {
                    if (IsSeparator(currentPath[i]))
                    {
                        do
                        {
                            i++;
                        } while (IsSeparator(currentPath.GetOrNull(i)));

                        if (IsNul(currentPath.GetOrNull(i)))
                            break;

                        if (!skipNextSep)
                        {
                            if (totalLength + 1 == outputBuffer.Length)
                            {
                                outputBuffer[totalLength] = NullTerminator;
                                normalizedLength = totalLength;

                                return ResultFs.TooLongPath.Log();
                            }

                            outputBuffer[totalLength++] = DirectorySeparator;
                        }

                        skipNextSep = false;
                    }

                    int dirLen = 0;
                    while (!IsSeparator(currentPath.GetOrNull(i + dirLen)) && !IsNul(currentPath.GetOrNull(i + dirLen)))
                    {
                        dirLen++;
                    }

                    if (IsCurrentDirectory(currentPath.Slice(i)))
                    {
                        skipNextSep = true;
                    }
                    else if (IsParentDirectory(currentPath.Slice(i)))
                    {
                        Assert.SdkAssert(outputBuffer[totalLength - 1] == DirectorySeparator);
                        Assert.SdkAssert(outputBuffer[prefixLength] == DirectorySeparator);

                        if (totalLength == prefixLength + 1)
                        {
                            if (!isUncPath)
                                return ResultFs.DirectoryUnobtainable.Log();

                            totalLength--;
                        }
                        else
                        {
                            totalLength -= 2;

                            do
                            {
                                if (outputBuffer[totalLength] == DirectorySeparator)
                                    break;

                                totalLength--;
                            } while (totalLength != prefixLength);
                        }

                        if (!isUncPath)
                            Assert.SdkAssert(outputBuffer[totalLength] == DirectorySeparator);

                        Assert.SdkAssert(totalLength < outputBuffer.Length);
                    }
                    else
                    {
                        if (totalLength + dirLen + 1 > outputBuffer.Length)
                        {
                            int copyLen = outputBuffer.Length - 1 - totalLength;

                            for (int j = 0; j < copyLen; j++)
                            {
                                outputBuffer[totalLength++] = currentPath[i + j];
                            }

                            outputBuffer[totalLength] = NullTerminator;
                            normalizedLength = totalLength;
                            return ResultFs.TooLongPath.Log();
                        }
                        else
                        {
                            for (int j = 0; j < dirLen; j++)
                            {
                                outputBuffer[totalLength++] = currentPath[i + j];
                            }
                        }
                    }

                    i += dirLen;
                }

                if (skipNextSep)
                    totalLength--;

                if (!isUncPath && totalLength == prefixLength && totalLength < outputBuffer.Length)
                {
                    outputBuffer[prefixLength] = DirectorySeparator;
                    totalLength++;
                }

                if (totalLength - 1 > outputBuffer.Length)
                {
                    return ResultFs.TooLongPath.Log();
                }
                else
                {
                    outputBuffer[totalLength] = NullTerminator;
                    normalizedLength = totalLength;

                    Assert.SdkAssert(IsNormalized(out bool normalized, new U8Span(outputBuffer), preserveUnc, hasMountName).IsSuccess());
                    Assert.SdkAssert(normalized);
                }

                return Result.Success;
            }
            finally
            {
                convertedPath.Dispose();
            }
        }

        public static Result IsNormalized(out bool isNormalized, U8Span path, bool preserveUnc, bool hasMountName)
        {
            UnsafeHelpers.SkipParamInit(out isNormalized);

            U8Span currentPath = path;
            U8Span originalPath = path;
            bool isUncPath = false;

            if (hasMountName)
            {
                Result rc = SkipMountName(out currentPath, originalPath);
                if (rc.IsFailure()) return rc;

                if (currentPath.GetOrNull(0) != DirectorySeparator)
                    return ResultFs.InvalidPathFormat.Log();
            }

            if (preserveUnc)
            {
                originalPath = currentPath;

                Result rc = SkipWindowsPath(out currentPath, out bool isUncNormalized, currentPath, hasMountName);
                if (rc.IsFailure()) return rc;

                if (!isUncNormalized)
                {
                    isNormalized = false;
                    return Result.Success;
                }

                // Path is a UNC path if the new path skips part of the original
                isUncPath = originalPath.Value != currentPath.Value;

                if (isUncPath)
                {
                    if (IsSeparator(originalPath.GetOrNull(0)) && IsSeparator(originalPath.GetOrNull(1)))
                    {
                        isNormalized = false;
                        return Result.Success;
                    }

                    if (IsNul(currentPath.GetOrNull(0)))
                    {
                        isNormalized = true;
                        return Result.Success;
                    }
                }
            }

            if (IsParentDirectoryPathReplacementNeeded(currentPath))
            {
                isNormalized = false;
                return Result.Success;
            }

            var state = NormalizeState.Initial;

            for (int i = 0; i < currentPath.Length; i++)
            {
                byte c = currentPath[i];
                if (c == NullTerminator) break;

                switch (state)
                {
                    case NormalizeState.Initial:
                        if (c == DirectorySeparator)
                        {
                            state = NormalizeState.FirstSeparator;
                        }
                        else
                        {
                            if (currentPath.Value == originalPath.Value)
                                return ResultFs.InvalidPathFormat.Log();

                            state = c == Dot ? NormalizeState.Dot : NormalizeState.Normal;
                        }

                        break;
                    case NormalizeState.Normal:
                        if (c == DirectorySeparator)
                        {
                            state = NormalizeState.Separator;
                        }

                        break;
                    case NormalizeState.FirstSeparator:
                    case NormalizeState.Separator:
                        if (c == DirectorySeparator)
                        {
                            isNormalized = false;
                            return Result.Success;
                        }

                        state = c == Dot ? NormalizeState.Dot : NormalizeState.Normal;
                        break;
                    case NormalizeState.Dot:
                        if (c == DirectorySeparator)
                        {
                            isNormalized = false;
                            return Result.Success;
                        }

                        state = c == Dot ? NormalizeState.DoubleDot : NormalizeState.Normal;
                        break;
                    case NormalizeState.DoubleDot:
                        if (c == DirectorySeparator)
                        {
                            isNormalized = false;
                            return Result.Success;
                        }

                        state = NormalizeState.Normal;
                        break;
                    // ReSharper disable once UnreachableSwitchCaseDueToIntegerAnalysis
                    default:
                        Abort.UnexpectedDefault();
                        break;
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
                // ReSharper disable once UnreachableSwitchCaseDueToIntegerAnalysis
                default:
                    Abort.UnexpectedDefault();
                    break;
            }

            return Result.Success;
        }

        public static bool IsCurrentDirectory(ReadOnlySpan<byte> p)
        {
            if (p.Length < 1)
                return false;

            ref byte b = ref MemoryMarshal.GetReference(p);

            return b == Dot &&
                   (p.Length == 1 || Unsafe.Add(ref b, 1) == NullTerminator ||
                    Unsafe.Add(ref b, 1) == DirectorySeparator);
        }

        public static bool IsParentDirectory(ReadOnlySpan<byte> p)
        {
            if (p.Length < 2)
                return false;

            ref byte b = ref MemoryMarshal.GetReference(p);

            return b == Dot &&
                   Unsafe.Add(ref b, 1) == Dot &&
                   (p.Length == 2 || Unsafe.Add(ref b, 2) == NullTerminator ||
                    Unsafe.Add(ref b, 2) == DirectorySeparator);
        }

        public static bool IsNul(byte c)
        {
            return c == NullTerminator;
        }

        public static bool IsSeparator(byte c)
        {
            return c == DirectorySeparator;
        }

        public static bool IsAnySeparator(byte c)
        {
            return c == DirectorySeparator || c == AltDirectorySeparator;
        }
    }
}
