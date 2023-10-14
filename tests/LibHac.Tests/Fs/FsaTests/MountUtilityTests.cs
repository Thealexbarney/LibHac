using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.Fs.Shim;
using LibHac.Tests.Fs.FileSystemClientTests;
using LibHac.Tools.Fs;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Fs.FsaTests;

public class MountUtilityTests
{
    [Theory]
    [InlineData("0123456789ABCDE", "0123456789ABCDE:/")]
    [InlineData("01234", "01234:/")]
    public void GetMountName_ValidName_ReturnsSuccess(string mountName, string path)
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.MountSdCard(mountName.ToU8Span()));
        Assert.Success(fs.GetEntryType(out _, path.ToU8Span()));
    }

    [Theory]
    [InlineData("01234", "01234")]
    [InlineData("0123456789ABCDE", "0123456789ABCDE")]
    [InlineData("01234", "0123456789ABCDEF")]
    [InlineData("01234", "0123456789ABCDEF:/")]
    public void GetMountName_InvalidName_ReturnsInvalidMountName(string mountName, string path)
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        Assert.Success(fs.MountSdCard(mountName.ToU8Span()));
        Assert.Result(ResultFs.InvalidMountName, fs.GetEntryType(out _, path.ToU8Span()));
    }

    private class TestCommonMountNameGenerator : ICommonMountNameGenerator
    {
        public void Dispose() { }

        public Result GenerateCommonMountName(Span<byte> nameBuffer)
        {
            ReadOnlySpan<byte> mountName = "@Test"u8;
            
            // Add 2 for the mount name separator and null terminator
            int requiredNameBufferSize = StringUtils.GetLength(mountName, PathTool.MountNameLengthMax) + 2;

            Assert.True(nameBuffer.Length >= requiredNameBufferSize);

            var sb = new U8StringBuilder(nameBuffer);
            sb.Append(mountName).Append(StringTraits.DriveSeparator);

            Assert.Equal(sb.Length, requiredNameBufferSize - 1);

            return Result.Success;
        }
    }

    [Fact]
    public void ConvertToFsCommonPath_MountedWithCommonPath_ReturnsCommonPath()
    {
        FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

        using var fileSystem = new UniqueRef<IFileSystem>(new InMemoryFileSystem());
        using var mountNameGenerator = new UniqueRef<ICommonMountNameGenerator>(new TestCommonMountNameGenerator());
        Assert.Success(fs.Register("mount"u8, ref fileSystem.Ref, ref mountNameGenerator.Ref));
        
        byte[] outputPath = new byte[100];
        Assert.Success(fs.ConvertToFsCommonPath(new U8SpanMutable(outputPath), "mount:/entry1/entry2"u8));

        string expected = "@Test:/entry1/entry2";
        string actual = StringUtils.Utf8ZToString(outputPath);
        Assert.Equal(expected, actual);
    }
}