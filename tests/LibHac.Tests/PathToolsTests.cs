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

        [Theory]
        [MemberData(nameof(NormalizedPathTestItems))]
        public static void NormalizePath(string path, string expected)
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
