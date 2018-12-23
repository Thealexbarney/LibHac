using System;
using System.IO;
using System.Runtime.CompilerServices;

namespace LibHac.IO
{
    public static class PathTools
    {
        public static readonly char DirectorySeparator = '/';

        // Licensed to the .NET Foundation under one or more agreements.
        // The .NET Foundation licenses this file to you under the MIT license.
        // See the LICENSE file in the project root for more information.
        public static string Normalize(string inPath)
        {
            ReadOnlySpan<char> path = inPath.AsSpan();

            if (path.Length == 0) return DirectorySeparator.ToString();

            if (path[0] != DirectorySeparator)
            {
                throw new InvalidDataException($"{nameof(path)} must begin with '{DirectorySeparator}'");
            }

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
    }
}
