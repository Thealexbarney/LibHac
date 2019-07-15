﻿using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

#if HAS_FILE_SYSTEM_NAME
using System.IO.Enumeration;
#endif

namespace LibHac.Fs
{
    public static class PathTools
    {
        public static readonly char DirectorySeparator = '/';
        public static readonly char MountSeparator = ':';
        internal const int MountNameLength = 0xF;

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
            return ResultFs.InvalidMountName;
        }

        public static bool MatchesPattern(string searchPattern, string name, bool ignoreCase)
        {
#if HAS_FILE_SYSTEM_NAME
            return FileSystemName.MatchesSimpleExpression(searchPattern.AsSpan(),
                           name.AsSpan(), ignoreCase);
#else
            return Compatibility.FileSystemName.MatchesSimpleExpression(searchPattern.AsSpan(),
                name.AsSpan(), ignoreCase);
#endif
        }

        private static bool IsValidMountNameChar(char c)
        {
            c |= (char)0x20;
            return c >= 'a' && c <= 'z';
        }

        private static bool IsValidMountNameChar(byte c) => IsValidMountNameChar((char)c);

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
