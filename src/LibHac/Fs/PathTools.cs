using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

using static LibHac.Results;
using static LibHac.Fs.ResultsFs;

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
                        state = NormalizeState.Delimiter;

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
            if (path.Length == 0) return "/";

            int i = path.Length - 1;

            // A trailing separator should be ignored
            if (path[i] == '/') i--;

            while (i >= 0 && path[i] != '/') i--;

            if (i < 1) return "/";
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
                }
            }

            return state == NormalizeState.Normal || state == NormalizeState.Delimiter;
        }

        public static bool IsSubPath(ReadOnlySpan<char> rootPath, ReadOnlySpan<char> path)
        {
            Debug.Assert(IsNormalized(rootPath));
            Debug.Assert(IsNormalized(path));

            if (path.Length <= rootPath.Length) return false;

            for (int i = 0; i < rootPath.Length; i++)
            {
                if (rootPath[i] != path[i]) return false;
            }

            // The input root path might or might not have a trailing slash.
            // Both are treated the same.
            int rootLength = rootPath[rootPath.Length - 1] == DirectorySeparator
                ? rootPath.Length - 1
                : rootPath.Length;

            // Return true if the character after the root path is a separator,
            // and if the possible sub path continues past that point.
            return path[rootLength] == DirectorySeparator && path.Length > rootLength + 1;
        }

        public static bool IsSubPath(ReadOnlySpan<byte> rootPath, ReadOnlySpan<byte> path)
        {
            Debug.Assert(IsNormalized(rootPath));
            Debug.Assert(IsNormalized(path));

            if (path.Length <= rootPath.Length) return false;

            for (int i = 0; i < rootPath.Length; i++)
            {
                if (rootPath[i] != path[i]) return false;
            }

            // The input root path might or might not have a trailing slash.
            // Both are treated the same.
            int rootLength = rootPath[rootPath.Length - 1] == DirectorySeparator
                ? rootPath.Length - 1
                : rootPath.Length;

            // Return true if the character after the root path is a separator,
            // and if the possible sub path continues past that point.
            return path[rootLength] == DirectorySeparator && path.Length > rootLength + 1;
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
            int maxLen = Math.Min(path.Length, MountNameLength);

            for (int i = 0; i < maxLen; i++)
            {
                if (path[i] == MountSeparator)
                {
                    mountName = path.Substring(0, i);
                    return ResultSuccess;
                }
            }

            mountName = default;
            return ResultFsInvalidMountName;
        }

        private enum NormalizeState
        {
            Initial,
            Normal,
            Delimiter,
            Dot,
            DoubleDot
        }
    }
}
