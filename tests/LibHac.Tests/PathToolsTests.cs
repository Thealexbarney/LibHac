using LibHac.IO;
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

        [Theory]
        [MemberData(nameof(NormalizedPathTestItems))]
        public static void NormalizePath(string path, string expected)
        {
            string actual = PathTools.Normalize(path);

            Assert.Equal(expected, actual);
        }
    }
}
