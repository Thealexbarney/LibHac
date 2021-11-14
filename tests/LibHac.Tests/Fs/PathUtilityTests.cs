// ReSharper disable InconsistentNaming

using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs;

public class PathUtilityTests
{
    public static TheoryData<string, string, bool> TestData_IsSubPath => new()
    {
        { @"//a/b", @"/a", false },
        { @"/a", @"//a/b", false },
        { @"//a/b", @"\\a", false },
        { @"//a/b", @"//a", true },
        { @"/", @"/a", true },
        { @"/a", @"/", true },
        { @"/", @"/", false },
        { @"", @"", false },
        { @"/", @"", true },
        { @"/", @"mount:/a", false },
        { @"mount:/", @"mount:/", false },
        { @"mount:/a/b", @"mount:/a/b", false },
        { @"mount:/a/b", @"mount:/a/b/c", true },
        { @"/a/b", @"/a/b/c", true },
        { @"/a/b/c", @"/a/b", true },
        { @"/a/b", @"/a/b", false },
        { @"/a/b", @"/a/b\c", false }
    };

    [Theory, MemberData(nameof(TestData_IsSubPath))]
    public static void IsSubPath(string path1, string path2, bool expectedResult)
    {
        bool result = PathUtility.IsSubPath(path1.ToU8Span(), path2.ToU8Span());

        Assert.Equal(expectedResult, result);
    }
}
