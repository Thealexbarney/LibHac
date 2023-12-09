// ReSharper disable InconsistentNaming

using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Fs;

public class PathFormatterTests
{
    public static TheoryData<string, string, string, Result> TestData_Normalize_EmptyPath => new()
    {
        { @"", "", @"", ResultFs.InvalidPathFormat.Value },
        { @"", "E", @"", Result.Success },
        { @"/aa/bb/../cc", "E", @"/aa/cc", Result.Success }
    };

    [Theory, MemberData(nameof(TestData_Normalize_EmptyPath))]
    public static void Normalize_EmptyPath(string path, string pathFlags, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, 0x301, expectedNormalized, expectedResult);
    }

    public static TheoryData<string, string, string, Result> TestData_Normalize_MountName => new()
    {
        { @"mount:/aa/bb", "", @"", ResultFs.InvalidPathFormat.Value },
        { @"mount:/aa/bb", "W", @"", ResultFs.InvalidPathFormat.Value },
        { @"mount:/aa/bb", "M", @"mount:/aa/bb", Result.Success },
        { @"mount:/aa/./bb", "M", @"mount:/aa/bb", Result.Success },
        { @"mount:\aa\bb", "M", @"mount:", ResultFs.InvalidPathFormat.Value },
        { @"m:/aa/bb", "M", @"", ResultFs.InvalidPathFormat.Value },
        { @"mo>unt:/aa/bb", "M", @"", ResultFs.InvalidCharacter.Value },
        { @"moun?t:/aa/bb", "M", @"", ResultFs.InvalidCharacter.Value },
        { @"mo&unt:/aa/bb", "M", @"mo&unt:/aa/bb", Result.Success },
        { @"/aa/./bb", "M", @"/aa/bb", Result.Success },
        { @"mount/aa/./bb", "M", @"", ResultFs.InvalidPathFormat.Value }
    };

    [Theory, MemberData(nameof(TestData_Normalize_MountName))]
    public static void Normalize_MountName(string path, string pathFlags, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, 0x301, expectedNormalized, expectedResult);
    }

    public static TheoryData<string, string, string, Result> TestData_Normalize_WindowsPath => new()
    {
        { @"c:/aa/bb", "", @"", ResultFs.InvalidPathFormat.Value },
        { @"c:\aa\bb", "", @"", ResultFs.InvalidCharacter.Value },
        { @"\\host\share", "", @"", ResultFs.InvalidCharacter.Value },
        { @"\\.\c:\", "", @"", ResultFs.InvalidCharacter.Value },
        { @"\\.\c:/aa/bb/.", "", @"", ResultFs.InvalidCharacter.Value },
        { @"\\?\c:\", "", @"", ResultFs.InvalidCharacter.Value },
        { @"mount:\\host\share\aa\bb", "M", @"mount:", ResultFs.InvalidCharacter.Value },
        { @"mount:\\host/share\aa\bb", "M", @"mount:", ResultFs.InvalidCharacter.Value },
        { @"c:\aa\..\..\..\bb", "W", @"c:/bb", Result.Success },
        { @"mount:/\\aa\..\bb", "MW", @"mount:", ResultFs.InvalidPathFormat.Value },
        { @"mount:/c:\aa\..\bb", "MW", @"mount:c:/bb", Result.Success },
        { @"mount:/aa/bb", "MW", @"mount:/aa/bb", Result.Success },
        { @"/mount:/aa/bb", "MW", @"/", ResultFs.InvalidCharacter.Value },
        { @"/mount:/aa/bb", "W", @"/", ResultFs.InvalidCharacter.Value },
        { @"a:aa/../bb", "MW", @"a:aa/bb", Result.Success },
        { @"a:aa\..\bb", "MW", @"a:aa/bb", Result.Success },
        { @"/a:aa\..\bb", "W", @"/", ResultFs.InvalidCharacter.Value },
        { @"\\?\c:\.\aa", "W", @"\\?\c:/aa", Result.Success },
        { @"\\.\c:\.\aa", "W", @"\\.\c:/aa", Result.Success },
        { @"\\.\mount:\.\aa", "W", @"\\./", ResultFs.InvalidCharacter.Value },
        { @"\\./.\aa", "W", @"\\./aa", Result.Success },
        { @"\\/aa", "W", @"", ResultFs.InvalidPathFormat.Value },
        { @"\\\aa", "W", @"", ResultFs.InvalidPathFormat.Value },
        { @"\\", "W", @"/", Result.Success },
        { @"\\host\share", "W", @"\\host\share/", Result.Success },
        { @"\\host\share\path", "W", @"\\host\share/path", Result.Success },
        { @"\\host\share\path\aa\bb\..\cc\.", "W", @"\\host\share/path/aa/cc", Result.Success },
        { @"\\host\", "W", @"", ResultFs.InvalidPathFormat.Value },
        { @"\\ho$st\share\path", "W", @"", ResultFs.InvalidCharacter.Value },
        { @"\\host:\share\path", "W", @"", ResultFs.InvalidCharacter.Value },
        { @"\\..\share\path", "W", @"", ResultFs.InvalidPathFormat.Value },
        { @"\\host\s:hare\path", "W", @"", ResultFs.InvalidCharacter.Value },
        { @"\\host\.\path", "W", @"", ResultFs.InvalidPathFormat.Value },
        { @"\\host\..\path", "W", @"", ResultFs.InvalidPathFormat.Value },
        { @"\\host\sha:re", "W", @"", ResultFs.InvalidCharacter.Value },
        { @".\\host\share", "RW", @"..\\host\share/", Result.Success }
    };

    [Theory, MemberData(nameof(TestData_Normalize_WindowsPath))]
    public static void Normalize_WindowsPath(string path, string pathFlags, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, 0x301, expectedNormalized, expectedResult);
    }

    public static TheoryData<string, string, string, Result> TestData_Normalize_RelativePath => new()
    {
        { @"./aa/bb", "", @"", ResultFs.InvalidPathFormat.Value },
        { @"./aa/bb/../cc", "R", @"./aa/cc", Result.Success },
        { @".\aa/bb/../cc", "R", @"..", ResultFs.InvalidCharacter.Value },
        { @".", "R", @".", Result.Success },
        { @"../aa/bb", "R", @"", ResultFs.DirectoryUnobtainable.Value },
        { @"/aa/./bb", "R", @"/aa/bb", Result.Success },
        { @"mount:./aa/bb", "MR", @"mount:./aa/bb", Result.Success },
        { @"mount:./aa/./bb", "MR", @"mount:./aa/bb", Result.Success },
        { @"mount:./aa/bb", "M", @"mount:", ResultFs.InvalidPathFormat.Value }
    };

    [Theory, MemberData(nameof(TestData_Normalize_RelativePath))]
    public static void Normalize_RelativePath(string path, string pathFlags, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, 0x301, expectedNormalized, expectedResult);
    }

    public static TheoryData<string, string, string, Result> TestData_Normalize_Backslash => new()
    {
        { @"\aa\bb\..\cc", "", @"", ResultFs.InvalidPathFormat.Value },
        { @"\aa\bb\..\cc", "B", @"", ResultFs.InvalidPathFormat.Value },
        { @"/aa\bb\..\cc", "", @"", ResultFs.InvalidCharacter.Value },
        { @"/aa\bb\..\cc", "B", @"/cc", Result.Success },
        { @"/aa\bb\cc", "", @"", ResultFs.InvalidCharacter.Value },
        { @"/aa\bb\cc", "B", @"/aa\bb\cc", Result.Success },
        { @"\\host\share\path\aa\bb\cc", "W", @"\\host\share/path/aa/bb/cc", Result.Success },
        { @"\\host\share\path\aa\bb\cc", "WB", @"\\host\share/path/aa/bb/cc", Result.Success },
        { @"/aa/bb\../cc/..\dd\..\ee/..", "", @"", ResultFs.InvalidCharacter.Value },
        { @"/aa/bb\../cc/..\dd\..\ee/..", "B", @"/aa", Result.Success }
    };

    [Theory, MemberData(nameof(TestData_Normalize_Backslash))]
    public static void Normalize_Backslash(string path, string pathFlags, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, 0x301, expectedNormalized, expectedResult);
    }

    public static TheoryData<string, string, string, Result> TestData_Normalize_AllowAllChars => new()
    {
        { @"/aa/b:b/cc", "", @"/aa/", ResultFs.InvalidCharacter.Value },
        { @"/aa/b*b/cc", "", @"/aa/", ResultFs.InvalidCharacter.Value },
        { @"/aa/b?b/cc", "", @"/aa/", ResultFs.InvalidCharacter.Value },
        { @"/aa/b<b/cc", "", @"/aa/", ResultFs.InvalidCharacter.Value },
        { @"/aa/b>b/cc", "", @"/aa/", ResultFs.InvalidCharacter.Value },
        { @"/aa/b|b/cc", "", @"/aa/", ResultFs.InvalidCharacter.Value },
        { @"/aa/b:b/cc", "C", @"/aa/b:b/cc", Result.Success },
        { @"/aa/b*b/cc", "C", @"/aa/b*b/cc", Result.Success },
        { @"/aa/b?b/cc", "C", @"/aa/b?b/cc", Result.Success },
        { @"/aa/b<b/cc", "C", @"/aa/b<b/cc", Result.Success },
        { @"/aa/b>b/cc", "C", @"/aa/b>b/cc", Result.Success },
        { @"/aa/b|b/cc", "C", @"/aa/b|b/cc", Result.Success },
        { @"/aa/b'b/cc", "", @"/aa/b'b/cc", Result.Success },
        { @"/aa/b""b/cc", "", @"/aa/b""b/cc", Result.Success },
        { @"/aa/b(b/cc", "", @"/aa/b(b/cc", Result.Success },
        { @"/aa/b)b/cc", "", @"/aa/b)b/cc", Result.Success },
        { @"/aa/b'b/cc", "C", @"/aa/b'b/cc", Result.Success },
        { @"/aa/b""b/cc", "C", @"/aa/b""b/cc", Result.Success },
        { @"/aa/b(b/cc", "C", @"/aa/b(b/cc", Result.Success },
        { @"/aa/b)b/cc", "C", @"/aa/b)b/cc", Result.Success },
        { @"mount:/aa/b<b/cc", "MC", @"mount:/aa/b<b/cc", Result.Success },
        { @"mo>unt:/aa/bb/cc", "MC", @"", ResultFs.InvalidCharacter.Value }
    };

    [Theory, MemberData(nameof(TestData_Normalize_AllowAllChars))]
    public static void Normalize_AllowAllChars(string path, string pathFlags, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, 0x301, expectedNormalized, expectedResult);
    }

    public static TheoryData<string, string, string, Result> TestData_Normalize_All => new()
    {
        { @"mount:./aa/bb", "WRM", @"mount:./aa/bb", Result.Success },
        { @"mount:./aa/bb\cc/dd", "WRM", @"mount:./aa/bb/cc/dd", Result.Success },
        { @"mount:./aa/bb\cc/dd", "WRMB", @"mount:./aa/bb/cc/dd", Result.Success },
        { @"mount:./.c:/aa/bb", "RM", @"mount:./", ResultFs.InvalidCharacter.Value },
        { @"mount:.c:/aa/bb", "WRM", @"mount:./", ResultFs.InvalidCharacter.Value },
        { @"mount:./cc:/aa/bb", "WRM", @"mount:./", ResultFs.InvalidCharacter.Value },
        { @"mount:./\\host\share/aa/bb", "MW", @"mount:", ResultFs.InvalidPathFormat.Value },
        { @"mount:./\\host\share/aa/bb", "WRM", @"mount:.\\host\share/aa/bb", Result.Success },
        { @"mount:.\\host\share/aa/bb", "WRM", @"mount:..\\host\share/aa/bb", Result.Success },
        { @"mount:..\\host\share/aa/bb", "WRM", @"mount:.", ResultFs.DirectoryUnobtainable.Value },
        { @".\\host\share/aa/bb", "WRM", @"..\\host\share/aa/bb", Result.Success },
        { @"..\\host\share/aa/bb", "WRM", @".", ResultFs.DirectoryUnobtainable.Value },
        { @"mount:\\host\share/aa/bb", "MW", @"mount:\\host\share/aa/bb", Result.Success },
        { @"mount:\aa\bb", "BM", @"mount:", ResultFs.InvalidPathFormat.Value },
        { @"mount:/aa\bb", "BM", @"mount:/aa\bb", Result.Success },
        { @".//aa/bb", "RW", @"./aa/bb", Result.Success },
        { @"./aa/bb", "R", @"./aa/bb", Result.Success },
        { @"./c:/aa/bb", "RW", @"./", ResultFs.InvalidCharacter.Value },
        { @"mount:./aa/b:b\cc/dd", "WRMBC", @"mount:./aa/b:b/cc/dd", Result.Success }
    };

    [Theory, MemberData(nameof(TestData_Normalize_All))]
    public static void Normalize_All(string path, string pathFlags, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, 0x301, expectedNormalized, expectedResult);
    }

    public static TheoryData<string, string, int, string, Result> TestData_Normalize_SmallBuffer => new()
    {
        { @"/aa/bb", "M", 1, @"", ResultFs.TooLongPath.Value },
        { @"mount:/aa/bb", "MR", 6, @"", ResultFs.TooLongPath.Value },
        { @"mount:/aa/bb", "MR", 7, @"mount:", ResultFs.TooLongPath.Value },
        { @"aa/bb", "MR", 3, @"./", ResultFs.TooLongPath.Value },
        { @"\\host\share", "W", 13, @"\\host\share", ResultFs.TooLongPath.Value }
    };

    [Theory, MemberData(nameof(TestData_Normalize_SmallBuffer))]
    public static void Normalize_SmallBuffer(string path, string pathFlags, int bufferSize, string expectedNormalized, Result expectedResult)
    {
        NormalizeImpl(path, pathFlags, bufferSize, expectedNormalized, expectedResult);
    }

    private static void NormalizeImpl(string path, string pathFlags, int bufferSize, string expectedNormalized, Result expectedResult)
    {
        byte[] buffer = new byte[bufferSize];

        Result result = PathFormatter.Normalize(buffer, path.ToU8Span(), GetPathFlags(pathFlags));

        Assert.Equal(expectedResult, result);
        Assert.Equal(expectedNormalized, StringUtils.Utf8ZToString(buffer));
    }

    public static TheoryData<string, string, bool, long, Result> TestData_IsNormalized_EmptyPath => new()
    {
        { @"", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"", "E", true, 0, Result.Success },
        { @"/aa/bb/../cc", "E", false, 0, Result.Success }
    };

    [Theory, MemberData(nameof(TestData_IsNormalized_EmptyPath))]
    public static void IsNormalized_EmptyPath(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        IsNormalizedImpl(path, pathFlags, expectedIsNormalized, expectedLength, expectedResult);
    }

    public static TheoryData<string, string, bool, long, Result> TestData_IsNormalized_MountName => new()
    {
        { @"mount:/aa/bb", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"mount:/aa/bb", "W", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"mount:/aa/bb", "M", true, 12, Result.Success },
        { @"mount:/aa/./bb", "M", false, 6, Result.Success },
        { @"mount:\aa\bb", "M", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"m:/aa/bb", "M", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"mo>unt:/aa/bb", "M", false, 0, ResultFs.InvalidCharacter.Value },
        { @"moun?t:/aa/bb", "M", false, 0, ResultFs.InvalidCharacter.Value },
        { @"mo&unt:/aa/bb", "M", true, 13, Result.Success },
        { @"/aa/./bb", "M", false, 0, Result.Success },
        { @"mount/aa/./bb", "M", false, 0, ResultFs.InvalidPathFormat.Value }
    };

    [Theory, MemberData(nameof(TestData_IsNormalized_MountName))]
    public static void IsNormalized_MountName(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        IsNormalizedImpl(path, pathFlags, expectedIsNormalized, expectedLength, expectedResult);
    }

    public static TheoryData<string, string, bool, long, Result> TestData_IsNormalized_WindowsPath => new()
    {
        { @"c:/aa/bb", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"c:\aa\bb", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\host\share", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\.\c:\", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\.\c:/aa/bb/.", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\?\c:\", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"mount:\\host\share\aa\bb", "M", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"mount:\\host/share\aa\bb", "M", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"c:\aa\..\..\..\bb", "W", false, 0, Result.Success },
        { @"mount:/\\aa\..\bb", "MW", false, 0, Result.Success },
        { @"mount:/c:\aa\..\bb", "MW", false, 0, Result.Success },
        { @"mount:/aa/bb", "MW", true, 12, Result.Success },
        { @"/mount:/aa/bb", "MW", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/mount:/aa/bb", "W", false, 0, ResultFs.InvalidCharacter.Value },
        { @"a:aa/../bb", "MW", false, 8, Result.Success },
        { @"a:aa\..\bb", "MW", false, 0, Result.Success },
        { @"/a:aa\..\bb", "W", false, 0, Result.Success },
        { @"\\?\c:\.\aa", "W", false, 0, Result.Success },
        { @"\\.\c:\.\aa", "W", false, 0, Result.Success },
        { @"\\.\mount:\.\aa", "W", false, 0, Result.Success },
        { @"\\./.\aa", "W", false, 0, Result.Success },
        { @"\\/aa", "W", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\\aa", "W", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\", "W", false, 0, Result.Success },
        { @"\\host\share", "W", false, 0, Result.Success },
        { @"\\host\share\path", "W", false, 0, Result.Success },
        { @"\\host\share\path\aa\bb\..\cc\.", "W", false, 0, Result.Success },
        { @"\\host\", "W", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\ho$st\share\path", "W", false, 0, ResultFs.InvalidCharacter.Value },
        { @"\\host:\share\path", "W", false, 0, ResultFs.InvalidCharacter.Value },
        { @"\\..\share\path", "W", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\host\s:hare\path", "W", false, 0, ResultFs.InvalidCharacter.Value },
        { @"\\host\.\path", "W", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\host\..\path", "W", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\\host\sha:re", "W", false, 0, ResultFs.InvalidCharacter.Value },
        { @".\\host\share", "RW", false, 0, Result.Success }
    };

    [Theory, MemberData(nameof(TestData_IsNormalized_WindowsPath))]
    public static void IsNormalized_WindowsPath(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        IsNormalizedImpl(path, pathFlags, expectedIsNormalized, expectedLength, expectedResult);
    }

    public static TheoryData<string, string, bool, long, Result> TestData_IsNormalized_RelativePath => new()
    {
        { @"./aa/bb", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"./aa/bb/../cc", "R", false, 1, Result.Success },
        { @".\aa/bb/../cc", "R", false, 0, Result.Success },
        { @".", "R", true, 1, Result.Success },
        { @"../aa/bb", "R", false, 0, ResultFs.DirectoryUnobtainable.Value },
        { @"/aa/./bb", "R", false, 0, Result.Success },
        { @"mount:./aa/bb", "MR", true, 13, Result.Success },
        { @"mount:./aa/./bb", "MR", false, 7, Result.Success },
        { @"mount:./aa/bb", "M", false, 0, ResultFs.InvalidPathFormat.Value }
    };

    [Theory, MemberData(nameof(TestData_IsNormalized_RelativePath))]
    public static void IsNormalized_RelativePath(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        IsNormalizedImpl(path, pathFlags, expectedIsNormalized, expectedLength, expectedResult);
    }

    public static TheoryData<string, string, bool, long, Result> TestData_IsNormalized_Backslash => new()
    {
        { @"\aa\bb\..\cc", "", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"\aa\bb\..\cc", "B", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"/aa\bb\..\cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa\bb\..\cc", "B", false, 0, Result.Success },
        { @"/aa\bb\cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa\bb\cc", "B", true, 9, Result.Success },
        { @"\\host\share\path\aa\bb\cc", "W", false, 0, Result.Success },
        { @"\\host\share\path\aa\bb\cc", "WB", false, 0, Result.Success },
        { @"/aa/bb\../cc/..\dd\..\ee/..", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa/bb\../cc/..\dd\..\ee/..", "B", false, 0, Result.Success }
    };

    [Theory, MemberData(nameof(TestData_IsNormalized_Backslash))]
    public static void IsNormalized_Backslash(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        IsNormalizedImpl(path, pathFlags, expectedIsNormalized, expectedLength, expectedResult);
    }

    public static TheoryData<string, string, bool, long, Result> TestData_IsNormalized_AllowAllChars => new()
    {
        { @"/aa/b:b/cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa/b*b/cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa/b?b/cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa/b<b/cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa/b>b/cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa/b|b/cc", "", false, 0, ResultFs.InvalidCharacter.Value },
        { @"/aa/b:b/cc", "C", true, 10, Result.Success },
        { @"/aa/b*b/cc", "C", true, 10, Result.Success },
        { @"/aa/b?b/cc", "C", true, 10, Result.Success },
        { @"/aa/b<b/cc", "C", true, 10, Result.Success },
        { @"/aa/b>b/cc", "C", true, 10, Result.Success },
        { @"/aa/b|b/cc", "C", true, 10, Result.Success },
        { @"/aa/b'b/cc", "", true, 10, Result.Success },
        { @"/aa/b""b/cc", "", true, 10, Result.Success },
        { @"/aa/b(b/cc", "", true, 10, Result.Success },
        { @"/aa/b)b/cc", "", true, 10, Result.Success },
        { @"/aa/b'b/cc", "C", true, 10, Result.Success },
        { @"/aa/b""b/cc", "C", true, 10, Result.Success },
        { @"/aa/b(b/cc", "C", true, 10, Result.Success },
        { @"/aa/b)b/cc", "C", true, 10, Result.Success },
        { @"mount:/aa/b<b/cc", "MC", true, 16, Result.Success },
        { @"mo>unt:/aa/bb/cc", "MC", false, 0, ResultFs.InvalidCharacter.Value }
    };

    [Theory, MemberData(nameof(TestData_IsNormalized_AllowAllChars))]
    public static void IsNormalized_AllowAllChars(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        IsNormalizedImpl(path, pathFlags, expectedIsNormalized, expectedLength, expectedResult);
    }

    public static TheoryData<string, string, bool, long, Result> TestData_IsNormalized_All => new()
    {
        { @"mount:./aa/bb", "WRM", true, 13, Result.Success },
        { @"mount:./aa/bb\cc/dd", "WRM", false, 0, Result.Success },
        { @"mount:./aa/bb\cc/dd", "WRMB", true, 19, Result.Success },
        { @"mount:./.c:/aa/bb", "RM", false, 0, ResultFs.InvalidCharacter.Value },
        { @"mount:.c:/aa/bb", "WRM", false, 0, Result.Success },
        { @"mount:./cc:/aa/bb", "WRM", false, 0, ResultFs.InvalidCharacter.Value },
        { @"mount:./\\host\share/aa/bb", "MW", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"mount:./\\host\share/aa/bb", "WRM", false, 0, Result.Success },
        { @"mount:.\\host\share/aa/bb", "WRM", false, 0, Result.Success },
        { @"mount:..\\host\share/aa/bb", "WRM", false, 0, Result.Success },
        { @".\\host\share/aa/bb", "WRM", false, 0, Result.Success },
        { @"..\\host\share/aa/bb", "WRM", false, 0, Result.Success },
        { @"mount:\\host\share/aa/bb", "MW", true, 24, Result.Success },
        { @"mount:\aa\bb", "BM", false, 0, ResultFs.InvalidPathFormat.Value },
        { @"mount:/aa\bb", "BM", true, 12, Result.Success },
        { @".//aa/bb", "RW", false, 1, Result.Success },
        { @"./aa/bb", "R", true, 7, Result.Success },
        { @"./c:/aa/bb", "RW", false, 0, ResultFs.InvalidCharacter.Value },
        { @"mount:./aa/b:b\cc/dd", "WRMBC", true, 20, Result.Success }
    };

    [Theory, MemberData(nameof(TestData_IsNormalized_All))]
    public static void IsNormalized_All(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        IsNormalizedImpl(path, pathFlags, expectedIsNormalized, expectedLength, expectedResult);
    }

    private static void IsNormalizedImpl(string path, string pathFlags, bool expectedIsNormalized, long expectedLength,
        Result expectedResult)
    {
        Result result = PathFormatter.IsNormalized(out bool isNormalized, out int length, path.ToU8Span(),
            GetPathFlags(pathFlags));

        Assert.Equal(expectedResult, result);

        if (result.IsSuccess())
        {
            Assert.Equal(expectedIsNormalized, isNormalized);

            if (isNormalized)
            {
                Assert.Equal(expectedLength, length);
            }
        }
    }

    [Fact]
    public static void IsNormalized_InvalidUtf8()
    {
        ReadOnlySpan<byte> invalidUtf8 = new byte[] { 0x44, 0xE3, 0xAA, 0x55, 0x50 };

        Result result = PathFormatter.IsNormalized(out _, out _, invalidUtf8, new PathFlags());

        Assert.Result(ResultFs.InvalidPathFormat, result);
    }

    private static PathFlags GetPathFlags(string pathFlags)
    {
        var flags = new PathFlags();

        foreach (char c in pathFlags)
        {
            switch (c)
            {
                case 'B':
                    flags.AllowBackslash();
                    break;
                case 'E':
                    flags.AllowEmptyPath();
                    break;
                case 'M':
                    flags.AllowMountName();
                    break;
                case 'R':
                    flags.AllowRelativePath();
                    break;
                case 'W':
                    flags.AllowWindowsPath();
                    break;
                case 'C':
                    flags.AllowAllCharacters();
                    break;
                default:
                    throw new NotSupportedException();
            }
        }

        return flags;
    }
}