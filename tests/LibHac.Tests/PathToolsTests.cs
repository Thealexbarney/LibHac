using System;
using System.Collections.Generic;
using System.Linq;
using LibHac.Common;
using LibHac.Tools.FsSystem;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests;

public class PathToolsTests
{
    public static object[][] NormalizedPathTestItems =
    [
        ["", "/"],
        ["/", "/"],
        ["/.", "/"],
        ["/a/b/c", "/a/b/c"],
        ["/a/b/../c", "/a/c"],
        ["/a/b/c/..", "/a/b"],
        ["/a/b/c/.", "/a/b/c"],
        ["/a/../../..", "/"],
        ["/a/../../../a/b/c", "/a/b/c"],
        ["//a/b//.//c", "/a/b/c"],
        ["/../a/b/c/.", "/a/b/c"],
        ["/./aaa/bbb/ccc/.", "/aaa/bbb/ccc"],

        ["/a/b/c/", "/a/b/c/"],
        ["a/b/c/", "/a/b/c/"],
        ["/aa/./bb/../cc/", "/aa/cc/"],
        ["/./b/../c/", "/c/"],
        ["/a/../../../", "/"],
        ["//a/b//.//c/", "/a/b/c/"],
        ["/tmp/../", "/"],

        ["a", "/a"],
        ["a/../../../a/b/c", "/a/b/c"],
        ["./b/../c/", "/c/"],
        [".", "/"],
        ["..", "/"],
        ["../a/b/c/.", "/a/b/c"],
        ["./a/b/c/.", "/a/b/c"],

        ["a:/a/b/c", "a:/a/b/c"],
        ["mount:/a/b/../c", "mount:/a/c"],
        ["mount:", "mount:/"],
        ["abc:/a/../../../a/b/c", "abc:/a/b/c"],
        ["abc:/./b/../c/", "abc:/c/"],
        ["abc:/.", "abc:/"],
        ["abc:/..", "abc:/"],
        ["abc:/", "abc:/"],
        ["abc://a/b//.//c", "abc:/a/b/c"],
        ["abc:/././/././a/b//.//c", "abc:/a/b/c"],
        ["mount:/d./aa", "mount:/d./aa"],
    ];

    public static object[][] SubPathTestItems =
    [
        ["/", "/", false],
        ["/", "/a", true],
        ["/", "/a/", true],

        ["/a/b/c", "/a/b/c/d", true],
        ["/a/b/c/", "/a/b/c/d", true],

        ["/a/b/c", "/a/b/c", false],
        ["/a/b/c/", "/a/b/c/", false],
        ["/a/b/c/", "/a/b/c", false],
        ["/a/b/c", "/a/b/c/", false],

        ["/a/b/c/", "/a/b/cdef", false],
        ["/a/b/c", "/a/b/cdef", false],
        ["/a/b/c/", "/a/b/cd", false],

        ["mount:/", "mount:/", false],
        ["mount:/", "mount:/a", true],
        ["mount:/", "mount:/a/", true],

        ["mount:/a/b/c", "mount:/a/b/c/d", true],
        ["mount:/a/b/c/", "mount:/a/b/c/d", true],

        ["mount:/a/b/c", "mount:/a/b/c", false],
        ["mount:/a/b/c/", "mount:/a/b/c/", false],
        ["mount:/a/b/c/", "mount:/a/b/c", false],
        ["mount:/a/b/c", "mount:/a/b/c/", false],

        ["mount:/a/b/c/", "mount:/a/b/cdef", false],
        ["mount:/a/b/c", "mount:/a/b/cdef", false],
        ["mount:/a/b/c/", "mount:/a/b/cd", false],
    ];

    public static object[][] ParentDirectoryTestItems =
    [
        ["/", ""],
        ["/a", "/"],
        ["/aa/aabc/f", "/aa/aabc"],
        ["mount:/", ""],
        ["mount:/a", "mount:/"],
        ["mount:/aa/aabc/f", "mount:/aa/aabc"]
    ];

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

    public static object[][] GetFileNameTestItems =
    [
        ["/a/bb/ccc", "ccc"],
        ["/a/bb/ccc/", ""],
        ["/a/bb", "bb"],
        ["/a/bb/", ""],
        ["/a", "a"],
        ["/a/", ""],
        ["/", ""],
    ];

    [Theory]
    [MemberData(nameof(GetFileNameTestItems))]
    public static void GetFileNameTest(string path, string expected)
    {
        var u8Path = path.ToU8Span();

        ReadOnlySpan<byte> fileName = PathTools.GetFileName(u8Path);

        string actual = StringUtils.Utf8ZToString(fileName);

        Assert.Equal(expected, actual);
    }

    public static object[][] GetLastSegmentTestItems =
    [
        ["/a/bb/ccc", "ccc"],
        ["/a/bb/ccc/", "ccc"],
        ["/a/bb", "bb"],
        ["/a/bb/", "bb"],
        ["/a", "a"],
        ["/a/", "a"],
        ["/", ""],
    ];

    [Theory]
    [MemberData(nameof(GetLastSegmentTestItems))]
    public static void GetLastSegmentTest(string path, string expected)
    {
        var u8Path = path.ToU8Span();

        ReadOnlySpan<byte> fileName = PathTools.GetLastSegment(u8Path);

        string actual = StringUtils.Utf8ZToString(fileName);

        Assert.Equal(expected, actual);
    }
}