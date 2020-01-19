﻿using System;
using System.Diagnostics;
using System.IO.Enumeration;
using System.Runtime.CompilerServices;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSystem
{
    public static class PathTools
    {
        internal const char DirectorySeparator = '/';
        internal const char MountSeparator = ':';
        internal const int MountNameLength = 0xF;

        // Todo: Remove
        internal const int MaxPathLength = 0x300;

        public static string Normalize(string inPath)
        {
            if (IsNormalized(inPath.AsSpan())) return inPath;

            Span<char> initialBuffer = stackalloc char[0x200];
            var sb = new ValueStringBuilder(initialBuffer);

            int rootLen = 0;
            int maxMountLen = Math.Min(inPath.Length, MountNameLength);

            for (int i = 0; i < maxMountLen; i++)
            {
                if (inPath[i] == MountSeparator)
                {
                    rootLen = i + 1;
                    break;
                }

                if (IsDirectorySeparator(inPath[i]))
                {
                    break;
                }
            }

            bool isNormalized = NormalizeInternal(inPath.AsSpan(), rootLen, ref sb);

            string normalized = isNormalized ? inPath : sb.ToString();

            sb.Dispose();

            return normalized;
        }

        public static Result Normalize(out U8Span normalizedPath, U8Span path)
        {
            if (path.Length == 0)
            {
                normalizedPath = path;
                return Result.Success;
            }

            // Todo: optimize
            normalizedPath = new U8Span(Normalize(path.ToString()));

            return Result.Success;
        }

        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // See the LICENSE file in the project root for more information.
        internal static bool NormalizeInternal(ReadOnlySpan<char> path, int rootLength, ref ValueStringBuilder sb)
        {
            if (rootLength > 0)
            {
                sb.Append(path.Slice(0, rootLength));
            }

            bool isNormalized = true;

            var state = NormalizeState.Initial;

            for (int i = rootLength; i < path.Length; i++)
            {
                char c = path[i];

                switch (state)
                {
                    case NormalizeState.Initial when IsDirectorySeparator(c):
                        state = NormalizeState.Delimiter;
                        sb.Append(c);
                        break;

                    case NormalizeState.Initial when c == '.':
                        isNormalized = false;
                        state = NormalizeState.Dot;

                        sb.Append(DirectorySeparator);
                        sb.Append(c);
                        break;

                    case NormalizeState.Initial:
                        isNormalized = false;
                        state = NormalizeState.Normal;

                        sb.Append(DirectorySeparator);
                        sb.Append(c);
                        break;

                    case NormalizeState.Normal when IsDirectorySeparator(c):
                        state = NormalizeState.Delimiter;
                        sb.Append(c);
                        break;

                    case NormalizeState.Normal:
                        sb.Append(c);
                        break;

                    case NormalizeState.Delimiter when IsDirectorySeparator(c):
                        isNormalized = false;
                        break;

                    case NormalizeState.Delimiter when c == '.':
                        state = NormalizeState.Dot;
                        sb.Append(c);
                        break;

                    case NormalizeState.Delimiter:
                        state = NormalizeState.Normal;
                        sb.Append(c);
                        break;

                    case NormalizeState.Dot when IsDirectorySeparator(c):
                        isNormalized = false;
                        state = NormalizeState.Delimiter;
                        sb.Length -= 1;
                        break;

                    case NormalizeState.Dot when c == '.':
                        state = NormalizeState.DoubleDot;
                        sb.Append(c);
                        break;

                    case NormalizeState.Dot:
                        state = NormalizeState.Normal;
                        sb.Append(c);
                        break;

                    case NormalizeState.DoubleDot when IsDirectorySeparator(c):
                        isNormalized = false;
                        state = NormalizeState.Delimiter;

                        int s = sb.Length - 1;
                        int separators = 0;

                        for (; s > rootLength; s--)
                        {
                            if (IsDirectorySeparator(sb[s]))
                            {
                                separators++;

                                if (separators == 2) break;
                            }
                        }

                        sb.Length = s + 1;

                        break;

                    case NormalizeState.DoubleDot:
                        state = NormalizeState.Normal;
                        break;
                }
            }

            switch (state)
            {
                case NormalizeState.Dot:
                    isNormalized = false;
                    sb.Length -= 2;
                    break;

                case NormalizeState.DoubleDot:
                    isNormalized = false;

                    int s = sb.Length - 1;
                    int separators = 0;

                    for (; s > rootLength; s--)
                    {
                        if (IsDirectorySeparator(sb[s]))
                        {
                            separators++;

                            if (separators == 2) break;
                        }
                    }

                    sb.Length = s;

                    break;
            }

            if (sb.Length == rootLength)
            {
                sb.Append(DirectorySeparator);

                return false;
            }

            return isNormalized;
        }

        public static string GetParentDirectory(string path)
        {
            Debug.Assert(IsNormalized(path.AsSpan()));

            int i = path.Length - 1;

            // Handles non-mounted root paths
            if (i == 0) return string.Empty;

            // A trailing separator should be ignored
            if (path[i] == '/') i--;

            // Handles mounted root paths
            if (i >= 0 && path[i] == ':') return string.Empty;

            while (i >= 0 && path[i] != '/') i--;

            // Leave the '/' if the parent is the root directory
            if (i == 0 || i > 0 && path[i - 1] == ':') i++;

            return path.Substring(0, i);
        }

        public static ReadOnlySpan<byte> GetParentDirectory(ReadOnlySpan<byte> path)
        {
            Debug.Assert(IsNormalized(path));

            int i = path.Length - 1;

            // A trailing separator should be ignored
            if (path[i] == '/') i--;

            while (i >= 1 && path[i] != '/') i--;

            i = Math.Max(i, 1);
            return path.Slice(0, i);
        }

        /// <summary>
        /// Returns the name and extension parts of the given path. The returned ReadOnlySpan
        /// contains the characters of the path that follows the last separator in path.
        /// </summary>
        public static ReadOnlySpan<byte> GetFileName(ReadOnlySpan<byte> path)
        {
            Debug.Assert(IsNormalized(path));

            int i = path.Length;

            while (i >= 1 && path[i - 1] != '/') i--;

            i = Math.Max(i, 0);
            return path.Slice(i, path.Length - i);
        }

        public static ReadOnlySpan<byte> GetLastSegment(ReadOnlySpan<byte> path)
        {
            Debug.Assert(IsNormalized(path));

            if (path.Length == 0)
                return path;

            int endIndex = path[path.Length - 1] == DirectorySeparator ? path.Length - 1 : path.Length;
            int i = endIndex;

            while (i >= 1 && path[i - 1] != '/') i--;

            i = Math.Max(i, 0);
            return path.Slice(i, endIndex - i);
        }

        public static bool IsNormalized(ReadOnlySpan<char> path)
        {
            var state = NormalizeState.Initial;

            foreach (char c in path)
            {
                switch (state)
                {
                    case NormalizeState.Initial when c == '/': state = NormalizeState.Delimiter; break;
                    case NormalizeState.Initial when IsValidMountNameChar(c): state = NormalizeState.MountName; break;
                    case NormalizeState.Initial: return false;

                    case NormalizeState.Normal when c == '/': state = NormalizeState.Delimiter; break;

                    case NormalizeState.Delimiter when c == '/': return false;
                    case NormalizeState.Delimiter when c == '.': state = NormalizeState.Dot; break;
                    case NormalizeState.Delimiter: state = NormalizeState.Normal; break;

                    case NormalizeState.Dot when c == '/': return false;
                    case NormalizeState.Dot when c == '.': state = NormalizeState.DoubleDot; break;
                    case NormalizeState.Dot: state = NormalizeState.Normal; break;

                    case NormalizeState.DoubleDot when c == '/': return false;
                    case NormalizeState.DoubleDot: state = NormalizeState.Normal; break;

                    case NormalizeState.MountName when IsValidMountNameChar(c): break;
                    case NormalizeState.MountName when c == ':': state = NormalizeState.MountDelimiter; break;
                    case NormalizeState.MountName: return false;

                    case NormalizeState.MountDelimiter when c == '/': state = NormalizeState.Delimiter; break;
                    case NormalizeState.MountDelimiter: return false;
                }
            }

            return state == NormalizeState.Normal || state == NormalizeState.Delimiter;
        }

        public static bool IsNormalized(ReadOnlySpan<byte> path)
        {
            var state = NormalizeState.Initial;

            foreach (byte c in path)
            {
                switch (state)
                {
                    case NormalizeState.Initial when c == '/': state = NormalizeState.Delimiter; break;
                    case NormalizeState.Initial when IsValidMountNameChar(c): state = NormalizeState.MountName; break;
                    case NormalizeState.Initial: return false;

                    case NormalizeState.Normal when c == '/': state = NormalizeState.Delimiter; break;

                    case NormalizeState.Delimiter when c == '/': return false;
                    case NormalizeState.Delimiter when c == '.': state = NormalizeState.Dot; break;
                    case NormalizeState.Delimiter: state = NormalizeState.Normal; break;

                    case NormalizeState.Dot when c == '/': return false;
                    case NormalizeState.Dot when c == '.': state = NormalizeState.DoubleDot; break;
                    case NormalizeState.Dot: state = NormalizeState.Normal; break;

                    case NormalizeState.DoubleDot when c == '/': return false;
                    case NormalizeState.DoubleDot: state = NormalizeState.Normal; break;

                    case NormalizeState.MountName when IsValidMountNameChar(c): break;
                    case NormalizeState.MountName when c == ':': state = NormalizeState.MountDelimiter; break;
                    case NormalizeState.MountName: return false;

                    case NormalizeState.MountDelimiter when c == '/': state = NormalizeState.Delimiter; break;
                    case NormalizeState.MountDelimiter: return false;
                }
            }

            return state == NormalizeState.Normal || state == NormalizeState.Delimiter;
        }

        /// <summary>
        /// Checks if either of the 2 paths is a sub-path of the other. Input paths must be normalized.
        /// </summary>
        /// <param name="path1">The first path to be compared.</param>
        /// <param name="path2">The second path to be compared.</param>
        /// <returns></returns>
        public static bool IsSubPath(ReadOnlySpan<char> path1, ReadOnlySpan<char> path2)
        {
            Debug.Assert(IsNormalized(path1));
            Debug.Assert(IsNormalized(path2));

            if (path1.Length == 0 || path2.Length == 0) return true;

            //Ignore any trailing slashes
            if (path1[path1.Length - 1] == DirectorySeparator)
            {
                path1 = path1.Slice(0, path1.Length - 1);
            }

            if (path2[path2.Length - 1] == DirectorySeparator)
            {
                path2 = path2.Slice(0, path2.Length - 1);
            }

            ReadOnlySpan<char> shortPath = path1.Length < path2.Length ? path1 : path2;
            ReadOnlySpan<char> longPath = path1.Length < path2.Length ? path2 : path1;

            if (!shortPath.SequenceEqual(longPath.Slice(0, shortPath.Length)))
            {
                return false;
            }

            return longPath.Length > shortPath.Length + 1 && longPath[shortPath.Length] == DirectorySeparator;
        }

        /// <summary>
        /// Checks if either of the 2 paths is a sub-path of the other. Input paths must be normalized.
        /// </summary>
        /// <param name="path1">The first path to be compared.</param>
        /// <param name="path2">The second path to be compared.</param>
        /// <returns></returns>
        public static bool IsSubPath(ReadOnlySpan<byte> path1, ReadOnlySpan<byte> path2)
        {
            Debug.Assert(IsNormalized(path1));
            Debug.Assert(IsNormalized(path2));

            if (path1.Length == 0 || path2.Length == 0) return true;

            //Ignore any trailing slashes
            if (path1[path1.Length - 1] == DirectorySeparator)
            {
                path1 = path1.Slice(0, path1.Length - 1);
            }

            if (path2[path2.Length - 1] == DirectorySeparator)
            {
                path2 = path2.Slice(0, path2.Length - 1);
            }

            ReadOnlySpan<byte> shortPath = path1.Length < path2.Length ? path1 : path2;
            ReadOnlySpan<byte> longPath = path1.Length < path2.Length ? path2 : path1;

            if (!shortPath.SequenceEqual(longPath.Slice(0, shortPath.Length)))
            {
                return false;
            }

            return longPath.Length > shortPath.Length + 1 && longPath[shortPath.Length] == DirectorySeparator;
        }

        public static string Combine(string path1, string path2)
        {
            if (path1 == null || path2 == null) throw new NullReferenceException();

            if (string.IsNullOrEmpty(path1)) return path2;
            if (string.IsNullOrEmpty(path2)) return path1;

            bool hasSeparator = IsDirectorySeparator(path1[path1.Length - 1]) || IsDirectorySeparator(path2[0]);

            if (hasSeparator)
            {
                return path1 + path2;
            }

            return path1 + DirectorySeparator + path2;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDirectorySeparator(char c)
        {
            return c == DirectorySeparator;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsDirectorySeparator(byte c)
        {
            return c == DirectorySeparator;
        }

        public static Result GetMountName(string path, out string mountName)
        {
            Result rc = GetMountNameLength(path, out int length);

            if (rc.IsFailure())
            {
                mountName = default;
                return rc;
            }

            mountName = path.Substring(0, length);
            return Result.Success;
        }

        public static Result GetMountNameLength(string path, out int length)
        {
            int maxLen = Math.Min(path.Length, MountNameLength);

            for (int i = 0; i < maxLen; i++)
            {
                if (path[i] == MountSeparator)
                {
                    length = i;
                    return Result.Success;
                }
            }

            length = default;
            return ResultFs.InvalidMountName.Log();
        }

        public static bool MatchesPattern(string searchPattern, string name, bool ignoreCase)
        {
            return FileSystemName.MatchesSimpleExpression(searchPattern.AsSpan(),
                           name.AsSpan(), ignoreCase);
        }

        private static bool IsValidMountNameChar(char c)
        {
            c |= (char)0x20;
            return c >= 'a' && c <= 'z';
        }

        private static bool IsValidMountNameChar(byte c) => IsValidMountNameChar((char)c);

        private static Result GetPathRoot(out ReadOnlySpan<byte> afterRootPath, Span<byte> pathRootBuffer, out int outRootLength, ReadOnlySpan<byte> path)
        {
            outRootLength = 0;
            afterRootPath = path;

            if (path.Length == 0) return Result.Success;

            int mountNameStart;
            if (IsDirectorySeparator(path[0]))
            {
                mountNameStart = 1;
            }
            else
            {
                mountNameStart = 0;
            }

            int rootLength = 0;

            for (int i = mountNameStart; i < mountNameStart + MountNameLength; i++)
            {
                if (i >= path.Length || path[i] == 0) break;

                // Set the length to 0 if there's no mount name
                if (IsDirectorySeparator(path[i]))
                {
                    outRootLength = 0;
                    return Result.Success;
                }

                if (path[i] == MountSeparator)
                {
                    rootLength = i + 1;
                    break;
                }
            }

            if (mountNameStart >= rootLength - 1 || path[rootLength - 1] != MountSeparator)
            {
                return ResultFs.InvalidPathFormat.Log();
            }

            if (mountNameStart < rootLength)
            {
                for (int i = mountNameStart; i < rootLength; i++)
                {
                    if (path[i] == '.')
                    {
                        return ResultFs.InvalidCharacter.Log();
                    }
                }
            }

            if (!pathRootBuffer.IsEmpty)
            {
                if (rootLength > pathRootBuffer.Length)
                {
                    return ResultFs.TooLongPath.Log();
                }

                path.Slice(0, rootLength).CopyTo(pathRootBuffer);
            }

            afterRootPath = path.Slice(rootLength);
            outRootLength = rootLength;
            return Result.Success;
        }

        public static Result Normalize(Span<byte> normalizedPath, out int normalizedLength, ReadOnlySpan<byte> path, bool hasMountName)
        {
            normalizedLength = 0;

            int rootLength = 0;
            ReadOnlySpan<byte> mainPath = path;

            if (hasMountName)
            {
                Result pathRootRc = GetPathRoot(out mainPath, normalizedPath, out rootLength, path);
                if (pathRootRc.IsFailure()) return pathRootRc;
            }

            var sb = new PathBuilder(normalizedPath.Slice(rootLength));

            var state = NormalizeState.Initial;

            for (int i = 0; i < mainPath.Length; i++)
            {
                Result rc = Result.Success;
                byte c = mainPath[i];

                // Read input strings as null-terminated
                if (c == 0) break;

                switch (state)
                {
                    case NormalizeState.Initial when IsDirectorySeparator(c):
                        state = NormalizeState.Delimiter;
                        break;

                    case NormalizeState.Initial:
                        return ResultFs.InvalidPathFormat.Log();

                    case NormalizeState.Normal when IsDirectorySeparator(c):
                        state = NormalizeState.Delimiter;
                        break;

                    case NormalizeState.Normal:
                        rc = sb.Append(c);
                        break;

                    case NormalizeState.Delimiter when IsDirectorySeparator(c):
                        break;

                    case NormalizeState.Delimiter when c == '.':
                        state = NormalizeState.Dot;
                        rc = sb.AppendWithPrecedingSeparator(c);
                        break;

                    case NormalizeState.Delimiter:
                        state = NormalizeState.Normal;
                        rc = sb.AppendWithPrecedingSeparator(c);
                        break;

                    case NormalizeState.Dot when IsDirectorySeparator(c):
                        state = NormalizeState.Delimiter;
                        rc = sb.GoUpLevels(1);
                        break;

                    case NormalizeState.Dot when c == '.':
                        state = NormalizeState.DoubleDot;
                        rc = sb.Append(c);
                        break;

                    case NormalizeState.Dot:
                        state = NormalizeState.Normal;
                        rc = sb.Append(c);
                        break;

                    case NormalizeState.DoubleDot when IsDirectorySeparator(c):
                        state = NormalizeState.Delimiter;
                        rc = sb.GoUpLevels(2);
                        break;

                    case NormalizeState.DoubleDot:
                        state = NormalizeState.Normal;
                        break;
                }

                if (rc.IsFailure())
                {
                    if (rc == ResultFs.TooLongPath)
                    {
                        // Make sure pending delimiters are added to the string if possible
                        if (state == NormalizeState.Delimiter)
                        {
                            sb.Append((byte)DirectorySeparator);
                        }
                    }

                    normalizedLength = sb.Length;
                    sb.Terminate();
                    return rc;
                }
            }

            Result finalRc = Result.Success;

            switch (state)
            {
                case NormalizeState.Dot:
                    state = NormalizeState.Delimiter;
                    finalRc = sb.GoUpLevels(1);
                    break;

                case NormalizeState.DoubleDot:
                    state = NormalizeState.Delimiter;
                    finalRc = sb.GoUpLevels(2);
                    break;
            }

            // Add the pending delimiter if the path is empty
            // or if the path has only a mount name with no trailing delimiter
            if (state == NormalizeState.Delimiter && sb.Length == 0 ||
                rootLength > 0 && sb.Length == 0)
            {
                finalRc = sb.Append((byte)'/');
            }

            normalizedLength = sb.Length;
            sb.Terminate();

            return finalRc;
        }

        private enum NormalizeState
        {
            Initial,
            Normal,
            Delimiter,
            Dot,
            DoubleDot,
            MountName,
            MountDelimiter
        }
    }
}
