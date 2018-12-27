using System.IO;
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


            new object[] {"/a/b/c/", "/a/b/c/"},
            new object[] {"/a/./b/../c/", "/a/c/"},
            new object[] {"/a/../../../", "/"},
            new object[] {"//a/b//.//c/", "/a/b/c/"},
            new object[] {@"/tmp/../", @"/"},
        };

        [Theory]
        [MemberData(nameof(NormalizedPathTestItems))]
        public static void NormalizePath(string path, string expected)
        {
            string actual = PathTools.Normalize(path);

            Assert.Equal(expected, actual);
        }

        [Fact]
        public static void NormalizeThrowsOnInvalidStartChar()
        {
            Assert.Throws<InvalidDataException>(() => PathTools.Normalize(@"c:\a\b\c"));
        }
    }
}
