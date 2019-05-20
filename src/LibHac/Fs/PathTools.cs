using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace LibHac.Fs
{
    public static class PathTools
    {
        public static readonly char DirectorySeparator = '/';

        public static string Normalize(string inPath)
        {
            if (IsNormalized(inPath.AsSpan())) return inPath;
            return NormalizeInternal(inPath);
        }

        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // See the LICENSE file in the project root for more information.
        public static string NormalizeInternal(string inPath)
        {
            // Relative paths aren't a thing for IFileSystem, so assume all paths are absolute
            // and add a '/' to the beginning of the path if it doesn't already begin with one
            if (inPath.Length == 0 || !IsDirectorySeparator(inPath[0])) inPath = DirectorySeparator + inPath;

            ReadOnlySpan<char> path = inPath.AsSpan();

            if (path.Length == 0) return DirectorySeparator.ToString();

            Span<char> initialBuffer = stackalloc char[0x200];
            var sb = new ValueStringBuilder(initialBuffer);

            for (int i = 0; i < path.Length; i++)
            {
                char c = path[i];

                if (IsDirectorySeparator(c) && i + 1 < path.Length)
                {
                    // Skip this character if it's a directory separator and if the next character is, too,
                    // e.g. "parent//child" => "parent/child"
                    if (IsDirectorySeparator(path[i + 1])) continue;

                    // Skip this character and the next if it's referring to the current directory,
                    // e.g. "parent/./child" => "parent/child"
                    if (IsCurrentDirectory(path, i))
                    {
                        i++;
                        continue;
                    }

                    // Skip this character and the next two if it's referring to the parent directory,
                    // e.g. "parent/child/../grandchild" => "parent/grandchild"
                    if (IsParentDirectory(path, i))
                    {
                        // Unwind back to the last slash (and if there isn't one, clear out everything).
                        for (int s = sb.Length - 1; s >= 0; s--)
                        {
                            if (IsDirectorySeparator(sb[s]))
                            {
                                sb.Length = s;
                                break;
                            }
                        }

                        i += 2;
                        continue;
                    }
                }
                sb.Append(c);
            }

            // If we haven't changed the source path, return the original
            if (sb.Length == inPath.Length)
            {
                return inPath;
            }

            if (sb.Length == 0)
            {
                sb.Append(DirectorySeparator);
            }

            return sb.ToString();
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
            if(path1 == null || path2 == null) throw new NullReferenceException();

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
        internal static bool IsCurrentDirectory(ReadOnlySpan<char> path, int index)
        {
            return (index + 2 == path.Length || IsDirectorySeparator(path[index + 2])) &&
                   path[index + 1] == '.';
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static bool IsParentDirectory(ReadOnlySpan<char> path, int index)
        {
            return index + 2 < path.Length &&
                   (index + 3 == path.Length || IsDirectorySeparator(path[index + 3])) &&
                   path[index + 1] == '.' && path[index + 2] == '.';
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
