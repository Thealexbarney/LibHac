using System;
using System.Collections.Generic;
using System.Linq;
using LibHac.Common;
using LibHac.Fs;
using LibHac.FsSystem;
using Xunit;

namespace LibHac.Tests
{
    public class PathToolsTests
    {
        public static object[][] NormalizedPathTestItems =
        {
            new object[] {"", "/"},
            new object[] {"/", "/"},
            new object[] {"/.", "/"},
            new object[] {"/a/b/c", "/a/b/c"},
            new object[] {"/a/b/../c", "/a/c"},
            new object[] {"/a/b/c/..", "/a/b"},
            new object[] {"/a/b/c/.", "/a/b/c"},
            new object[] {"/a/../../..", "/"},
            new object[] {"/a/../../../a/b/c", "/a/b/c"},
            new object[] {"//a/b//.//c", "/a/b/c"},
            new object[] {"/../a/b/c/.", "/a/b/c"},
            new object[] {"/./aaa/bbb/ccc/.", "/aaa/bbb/ccc"},

            new object[] {"/a/b/c/", "/a/b/c/"},
            new object[] {"a/b/c/", "/a/b/c/"},
            new object[] {"/aa/./bb/../cc/", "/aa/cc/"},
            new object[] {"/./b/../c/", "/c/"},
            new object[] {"/a/../../../", "/"},
            new object[] {"//a/b//.//c/", "/a/b/c/"},
            new object[] {"/tmp/../", "/"},

            new object[] {"a", "/a"},
            new object[] {"a/../../../a/b/c", "/a/b/c"},
            new object[] {"./b/../c/", "/c/"},
            new object[] {".", "/"},
            new object[] {"..", "/"},
            new object[] {"../a/b/c/.", "/a/b/c"},
            new object[] {"./a/b/c/.", "/a/b/c"},

            new object[] {"a:/a/b/c", "a:/a/b/c"},
            new object[] {"mount:/a/b/../c", "mount:/a/c"},
            new object[] {"mount:", "mount:/"},
            new object[] {"abc:/a/../../../a/b/c", "abc:/a/b/c"},
            new object[] {"abc:/./b/../c/", "abc:/c/"},
            new object[] {"abc:/.", "abc:/"},
            new object[] {"abc:/..", "abc:/"},
            new object[] {"abc:/", "abc:/"},
            new object[] {"abc://a/b//.//c", "abc:/a/b/c"},
            new object[] {"abc:/././/././a/b//.//c", "abc:/a/b/c"},
            new object[] {"mount:/d./aa", "mount:/d./aa"},
        };

        public static object[][] SubPathTestItems =
        {
            new object[] {"/", "/", false},
            new object[] {"/", "/a", true},
            new object[] {"/", "/a/", true},

            new object[] {"/a/b/c", "/a/b/c/d", true},
            new object[] {"/a/b/c/", "/a/b/c/d", true},

            new object[] {"/a/b/c", "/a/b/c", false},
            new object[] {"/a/b/c/", "/a/b/c/", false},
            new object[] {"/a/b/c/", "/a/b/c", false},
            new object[] {"/a/b/c", "/a/b/c/", false},

            new object[] {"/a/b/c/", "/a/b/cdef", false},
            new object[] {"/a/b/c", "/a/b/cdef", false},
            new object[] {"/a/b/c/", "/a/b/cd", false},

            new object[] {"mount:/", "mount:/", false},
            new object[] {"mount:/", "mount:/a", true},
            new object[] {"mount:/", "mount:/a/", true},

            new object[] {"mount:/a/b/c", "mount:/a/b/c/d", true},
            new object[] {"mount:/a/b/c/", "mount:/a/b/c/d", true},

            new object[] {"mount:/a/b/c", "mount:/a/b/c", false},
            new object[] {"mount:/a/b/c/", "mount:/a/b/c/", false},
            new object[] {"mount:/a/b/c/", "mount:/a/b/c", false},
            new object[] {"mount:/a/b/c", "mount:/a/b/c/", false},

            new object[] {"mount:/a/b/c/", "mount:/a/b/cdef", false},
            new object[] {"mount:/a/b/c", "mount:/a/b/cdef", false},
            new object[] {"mount:/a/b/c/", "mount:/a/b/cd", false},
        };

        public static object[][] ParentDirectoryTestItems =
        {
            new object[] {"/", ""},
            new object[] {"/a", "/"},
            new object[] {"/aa/aabc/f", "/aa/aabc"},
            new object[] {"mount:/", ""},
            new object[] {"mount:/a", "mount:/"},
            new object[] {"mount:/aa/aabc/f", "mount:/aa/aabc"}
        };

        public static object[][] IsNormalizedTestItems = GetNormalizedPaths(true);

        public static object[][] IsNotNormalizedTestItems = GetNormalizedPaths(false);

        [Theory]
        [MemberData(nameof(NormalizedPathTestItems))]
        public static void NormalizePath(string path, string expected)
        {
            string actual = PathTools.Normalize(path);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(IsNormalizedTestItems))]
        public static void IsNormalized(string path)
        {
            Assert.True(PathTools.IsNormalized(path.AsSpan()));
        }

        [Theory]
        [MemberData(nameof(IsNotNormalizedTestItems))]
        public static void IsNotNormalized(string path)
        {
            Assert.False(PathTools.IsNormalized(path.AsSpan()));
        }

        [Theory]
        [MemberData(nameof(SubPathTestItems))]
        public static void TestSubPath(string rootPath, string path, bool expected)
        {
            bool actual = PathTools.IsSubPath(rootPath.AsSpan(), path.AsSpan());

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(SubPathTestItems))]
        public static void TestSubPathReverse(string rootPath, string path, bool expected)
        {
            bool actual = PathTools.IsSubPath(path.AsSpan(), rootPath.AsSpan());

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(ParentDirectoryTestItems))]
        public static void TestParentDirectory(string path, string expected)
        {
            string actual = PathTools.GetParentDirectory(path);

            Assert.Equal(expected, actual);
        }

        private static object[][] GetNormalizedPaths(bool getNormalized)
        {
            var normalizedPaths = new HashSet<string>();
            var notNormalizedPaths = new HashSet<string>();

            foreach (object[] pair in NormalizedPathTestItems)
            {
                string pathNotNorm = (string)pair[0];
                string pathNorm = (string)pair[1];

                if (pathNorm != pathNotNorm) notNormalizedPaths.Add(pathNotNorm);
                normalizedPaths.Add(pathNorm);
            }

            HashSet<string> paths = getNormalized ? normalizedPaths : notNormalizedPaths;

            return paths.Select(x => new object[] { x }).ToArray();
        }

        public static object[][] NormalizedPathTestItemsU8NoMountName =
        {
            new object[] {"/", "/", Result.Success},
            new object[] {"/.", "/", Result.Success},
            new object[] {"/..", "", ResultFs.DirectoryUnobtainable},
            new object[] {"/abc", "/abc", Result.Success},
            new object[] {"/a/..", "/", Result.Success},
            new object[] {"/a/b/c", "/a/b/c", Result.Success},
            new object[] {"/a/b/../c", "/a/c", Result.Success},
            new object[] {"/a/b/c/..", "/a/b", Result.Success},
            new object[] {"/a/b/c/.", "/a/b/c", Result.Success},
            new object[] {"/a/../../..", "", ResultFs.DirectoryUnobtainable},
            new object[] {"/a/../../../a/b/c", "", ResultFs.DirectoryUnobtainable},
            new object[] {"//a/b//.//c", "/a/b/c", Result.Success},
            new object[] {"/../a/b/c/.", "", ResultFs.DirectoryUnobtainable},
            new object[] {"/./aaa/bbb/ccc/.", "/aaa/bbb/ccc", Result.Success},

            new object[] {"/a/b/c/", "/a/b/c", Result.Success},
            new object[] {"/aa/./bb/../cc/", "/aa/cc", Result.Success},
            new object[] {"/./b/../c/", "/c", Result.Success},
            new object[] {"/a/../../../", "", ResultFs.DirectoryUnobtainable},
            new object[] {"//a/b//.//c/", "/a/b/c", Result.Success},
            new object[] {"/tmp/../", "/", Result.Success},
            new object[] {"abc", "", ResultFs.InvalidPathFormat}
        };

        public static object[][] NormalizedPathTestItemsU8MountName =
        {
            new object[] {"mount:/a/b/../c", "mount:/a/c", Result.Success},
            new object[] {"a:/a/b/c", "a:/a/b/c", Result.Success},
            new object[] {"mount:/a/b/../c", "mount:/a/c", Result.Success},
            new object[] {"mount:", "mount:/", Result.Success},
            new object[] {"abc:/a/../../../a/b/c", "", ResultFs.DirectoryUnobtainable},
            new object[] {"abc:/./b/../c/", "abc:/c", Result.Success},
            new object[] {"abc:/.", "abc:/", Result.Success},
            new object[] {"abc:/..", "", ResultFs.DirectoryUnobtainable},
            new object[] {"abc:/", "abc:/", Result.Success},
            new object[] {"abc://a/b//.//c", "abc:/a/b/c", Result.Success},
            new object[] {"abc:/././/././a/b//.//c", "abc:/a/b/c", Result.Success},
            new object[] {"mount:/d./aa", "mount:/d./aa", Result.Success},
            new object[] {"mount:/d/..", "mount:/", Result.Success}
        };

        [Theory]
        [MemberData(nameof(NormalizedPathTestItemsU8NoMountName))]
        public static void NormalizePathU8NoMountName(string path, string expected, Result expectedResult)
        {
            U8String u8Path = path.ToU8String();
            Span<byte> buffer = stackalloc byte[0x301];

            Result rc = PathTools.Normalize(buffer, out _, u8Path, false);

            string actual = StringUtils.Utf8ZToString(buffer);

            Assert.Equal(expectedResult, rc);
            if (expectedResult == Result.Success)
            {
                Assert.Equal(expected, actual);
            }
        }

        [Theory]
        [MemberData(nameof(NormalizedPathTestItemsU8MountName))]
        public static void NormalizePathU8MountName(string path, string expected, Result expectedResult)
        {
            U8String u8Path = path.ToU8String();
            Span<byte> buffer = stackalloc byte[0x301];

            Result rc = PathTools.Normalize(buffer, out _, u8Path, true);

            string actual = StringUtils.Utf8ZToString(buffer);

            Assert.Equal(expectedResult, rc);
            if (expectedResult == Result.Success)
            {
                Assert.Equal(expected, actual);
            }
        }

        public static object[][] NormalizedPathTestItemsU8TooShort =
        {
            new object[] {"/a/b/c", "", 0},
            new object[] {"/a/b/c", "/a/", 4},
            new object[] {"/a/b/c", "/a/b", 5},
            new object[] {"/a/b/c", "/a/b/", 6}
        };

        [Theory]
        [MemberData(nameof(NormalizedPathTestItemsU8TooShort))]
        public static void NormalizePathU8TooShortDest(string path, string expected, int destSize)
        {
            U8String u8Path = path.ToU8String();

            Span<byte> buffer = stackalloc byte[destSize];

            Result rc = PathTools.Normalize(buffer, out int normalizedLength, u8Path, false);

            string actual = StringUtils.Utf8ZToString(buffer);

            Assert.Equal(ResultFs.TooLongPath, rc);
            Assert.Equal(Math.Max(0, destSize - 1), normalizedLength);
            Assert.Equal(expected, actual);
        }
    }
}
