using System;
using LibHac.Common;
using LibHac.Diag;
using LibHac.FsSystem;
using static LibHac.Fs.PathUtility;
using static LibHac.Fs.StringTraits;

// ReSharper disable once CheckNamespace
namespace LibHac.Fs
{
    /// <summary>
    /// Contains functions for doing with basic path normalization.
    /// </summary>
    /// <remarks>Based on FS 12.1.0 (nnSdk 12.3.1)</remarks>
    public static class PathNormalizer
    {
        private enum PathState
        {
            Initial,
            Normal,
            FirstSeparator,
            Separator,
            CurrentDir,
            ParentDir
        }

        public static Result Normalize(Span<byte> outputBuffer, out int length, ReadOnlySpan<byte> path, bool isWindowsPath,
            bool isDriveRelativePath)
        {
            UnsafeHelpers.SkipParamInit(out length);

            ReadOnlySpan<byte> currentPath = path;
            int totalLength = 0;
            int i = 0;

            if (!IsSeparator(path.At(0)))
            {
                if (!isDriveRelativePath)
                    return ResultFs.InvalidPathFormat.Log();

                outputBuffer[totalLength++] = DirectorySeparator;
            }

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

                bool skipNextSeparator = false;

                while (!IsNul(currentPath.At(i)))
                {
                    if (IsSeparator(currentPath[i]))
                    {
                        do
                        {
                            i++;
                        } while (IsSeparator(currentPath.At(i)));

                        if (IsNul(currentPath.At(i)))
                            break;

                        if (!skipNextSeparator)
                        {
                            if (totalLength + 1 == outputBuffer.Length)
                            {
                                outputBuffer[totalLength] = NullTerminator;
                                length = totalLength;

                                return ResultFs.TooLongPath.Log();
                            }

                            outputBuffer[totalLength++] = DirectorySeparator;
                        }

                        skipNextSeparator = false;
                    }

                    int dirLen = 0;
                    while (!IsSeparator(currentPath.At(i + dirLen)) && !IsNul(currentPath.At(i + dirLen)))
                    {
                        dirLen++;
                    }

                    if (IsCurrentDirectory(currentPath.Slice(i)))
                    {
                        skipNextSeparator = true;
                    }
                    else if (IsParentDirectory(currentPath.Slice(i)))
                    {
                        Assert.SdkAssert(outputBuffer[totalLength - 1] == DirectorySeparator);

                        if (!isWindowsPath)
                            Assert.SdkAssert(outputBuffer[0] == DirectorySeparator);

                        if (totalLength == 1)
                        {
                            if (!isWindowsPath)
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
                            } while (totalLength != 0);
                        }

                        if (!isWindowsPath)
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
                            length = totalLength;
                            return ResultFs.TooLongPath.Log();
                        }

                        for (int j = 0; j < dirLen; j++)
                        {
                            outputBuffer[totalLength++] = currentPath[i + j];
                        }
                    }

                    i += dirLen;
                }

                if (skipNextSeparator)
                    totalLength--;

                if (totalLength == 0 && outputBuffer.Length != 0)
                {
                    totalLength = 1;
                    outputBuffer[0] = DirectorySeparator;
                }

                // Note: This bug is in the original code. They probably meant to put "totalLength + 1"
                if (totalLength - 1 > outputBuffer.Length)
                    return ResultFs.TooLongPath.Log();

                outputBuffer[totalLength] = NullTerminator;

                Result rc = IsNormalized(out bool isNormalized, out _, outputBuffer);
                if (rc.IsFailure()) return rc;

                Assert.SdkAssert(isNormalized);

                length = totalLength;
                return Result.Success;
            }
            finally
            {
                convertedPath.Dispose();
            }
        }

        /// <summary>
        /// Checks if a given path is normalized. Path must be a basic path, starting with a directory separator
        /// and not containing any sort of prefix such as a mount name.
        /// </summary>
        /// <param name="isNormalized">When this function returns <see cref="Result.Success"/>,
        /// contains <see langword="true"/> if the path is normalized or <see langword="false"/> if it is not.
        /// Contents are undefined if the function does not return <see cref="Result.Success"/>.
        /// </param>
        /// <param name="length">When this function returns <see cref="Result.Success"/> and
        /// <paramref name="isNormalized"/> is <see langword="true"/>, contains the length of the normalized path.
        /// Contents are undefined if the function does not return <see cref="Result.Success"/>
        /// or <paramref name="isNormalized"/> is <see langword="false"/>.
        /// </param>
        /// <param name="path">The path to check.</param>
        /// <returns><see cref="Result.Success"/>: The operation was successful.<br/>
        /// <see cref="ResultFs.InvalidCharacter"/>: The path contains an invalid character.<br/>
        /// <see cref="ResultFs.InvalidPathFormat"/>: The path is not in a valid format.</returns>
        public static Result IsNormalized(out bool isNormalized, out int length, ReadOnlySpan<byte> path)
        {
            UnsafeHelpers.SkipParamInit(out isNormalized, out length);

            var state = PathState.Initial;
            int pathLength = 0;

            for (int i = 0; i < path.Length; i++)
            {
                byte c = path[i];
                if (c == NullTerminator) break;

                pathLength++;

                if (state != PathState.Initial)
                {
                    Result rc = CheckInvalidCharacter(c);
                    if (rc.IsFailure()) return rc;
                }

                switch (state)
                {
                    case PathState.Initial:
                        if (c != DirectorySeparator)
                            return ResultFs.InvalidPathFormat.Log();

                        state = PathState.FirstSeparator;

                        break;
                    case PathState.Normal:

                        if (c == DirectorySeparator)
                            state = PathState.Separator;

                        break;
                    case PathState.FirstSeparator:
                    case PathState.Separator:
                        if (c == DirectorySeparator)
                        {
                            isNormalized = false;
                            return Result.Success;
                        }

                        state = c == Dot ? PathState.CurrentDir : PathState.Normal;
                        break;
                    case PathState.CurrentDir:
                        if (c == DirectorySeparator)
                        {
                            isNormalized = false;
                            return Result.Success;
                        }

                        state = c == Dot ? PathState.ParentDir : PathState.Normal;
                        break;
                    case PathState.ParentDir:
                        if (c == DirectorySeparator)
                        {
                            isNormalized = false;
                            return Result.Success;
                        }

                        state = PathState.Normal;
                        break;
                    // ReSharper disable once UnreachableSwitchCaseDueToIntegerAnalysis
                    default:
                        Abort.UnexpectedDefault();
                        break;
                }
            }

            switch (state)
            {
                case PathState.Initial:
                    return ResultFs.InvalidPathFormat.Log();
                case PathState.Normal:
                case PathState.FirstSeparator:
                    isNormalized = true;
                    break;
                case PathState.Separator:
                case PathState.CurrentDir:
                case PathState.ParentDir:
                    isNormalized = false;
                    break;
                // ReSharper disable once UnreachableSwitchCaseDueToIntegerAnalysis
                default:
                    Abort.UnexpectedDefault();
                    break;
            }

            length = pathLength;
            return Result.Success;
        }


        /// <summary>
        /// Checks if a path begins with / or \ and contains any of these patterns:
        /// "/..\", "\..\", "\../", "\..0" where '0' is the null terminator.
        /// </summary>
        public static bool IsParentDirectoryPathReplacementNeeded(ReadOnlySpan<byte> path)
        {
            if (path.Length == 0 || (path[0] != DirectorySeparator && path[0] != AltDirectorySeparator))
                return false;

            for (int i = 0; i < path.Length - 2 && path[i] != NullTerminator; i++)
            {
                byte c3 = path.At(i + 3);

                if (path[i] == AltDirectorySeparator &&
                    path[i + 1] == Dot &&
                    path[i + 2] == Dot &&
                    (c3 == DirectorySeparator || c3 == AltDirectorySeparator || c3 == NullTerminator))
                {
                    return true;
                }

                if ((path[i] == DirectorySeparator || path[i] == AltDirectorySeparator) &&
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
                    (source[i - 1] == DirectorySeparator || source[i - 1] == AltDirectorySeparator) &&
                     source[i + 0] == Dot &&
                     source[i + 1] == Dot &&
                    (source[i + 2] == DirectorySeparator || source[i + 2] == AltDirectorySeparator))
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
    }
}
