using System;
using LibHac.Fs;
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
        };

        public static object[][] IsNormalizedTestItems =
        {
            new object[] {"", "/"},
            new object[] {"/"},
            new object[] {"/a/b/c"},
            new object[] {"/a/c"},
            new object[] {"/a/b"},
            new object[] {"/a/b/c"},
            new object[] {"/"},
            new object[] {"/a/b/c"},

            new object[] {"/a/b/c/"},
            new object[] {"/a/c/"},
            new object[] {"/c/"},

            new object[] {"/a"},

            new object[] {"a:/a/b/c"},
            new object[] {"mount:/a/c"},
            new object[] {"mount:/"},
        };

        public static object[][] IsNotNormalizedTestItems =
        {
            new object[] {""},
            new object[] {"/."},
            new object[] {"/a/b/../c", "/a/c"},
            new object[] {"/a/b/c/..", "/a/b"},
            new object[] {"/a/b/c/.", "/a/b/c"},
            new object[] {"/a/../../..", "/"},
            new object[] {"/a/../../../a/b/c", "/a/b/c"},
            new object[] {"//a/b//.//c", "/a/b/c"},
            new object[] {"/../a/b/c/.", "/a/b/c"},
            new object[] {"/./a/b/c/.", "/a/b/c"},

            new object[] {"/a/b/c/", "/a/b/c/"},
            new object[] {"/a/./b/../c/", "/a/c/"},
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
            new object[] {"mount:/a/b/../c", "mount:/a/c"},
            new object[] {"mount:", "mount:/"},
            new object[] {"abc:/a/../../../a/b/c", "abc:/a/b/c"},
        };

        [Theory]
        [MemberData(nameof(NormalizedPathTestItems))]
        public static void NormalizePath(string path, string expected)
        {
            string actual = PathTools.Normalize(path);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(NormalizedPathTestItems))]
        public static void IsNormalized(string path, string expected)
        {
            string actual = PathTools.Normalize(path);

            Assert.Equal(expected, actual);
        }

        [Theory]
        [MemberData(nameof(SubPathTestItems))]
        public static void TestSubPath(string rootPath, string path, bool expected)
        {
            bool actual = PathTools.IsSubPath(rootPath.AsSpan(), path.AsSpan());

            Assert.Equal(expected, actual);
        }
    }
}
