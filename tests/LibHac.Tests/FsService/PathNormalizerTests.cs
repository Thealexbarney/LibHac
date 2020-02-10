using LibHac.Common;
using LibHac.Fs;
using LibHac.FsService;
using Xunit;

namespace LibHac.Tests.FsService
{
    public class PathNormalizerTests
    {
        [Fact]
        public static void Ctor_EmptyPathWithAcceptEmptyOption_ReturnsEmptyPathWithSuccess()
        {
            var normalizer = new PathNormalizer("".ToU8Span(), PathNormalizer.Option.AcceptEmpty);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.True(normalizer.Path.IsEmpty());
        }

        [Fact]
        public static void Normalize_PreserveTailSeparatorOption_KeepsExistingTailSeparator()
        {
            var normalizer = new PathNormalizer("/a/./b/".ToU8Span(), PathNormalizer.Option.PreserveTailSeparator);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal("/a/b/", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_PreserveTailSeparatorOption_IgnoresMissingTailSeparator()
        {
            var normalizer = new PathNormalizer("/a/./b".ToU8Span(), PathNormalizer.Option.PreserveTailSeparator);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal("/a/b", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_PathAlreadyNormalized_ReturnsSameBuffer()
        {
            var originalPath = "/a/b".ToU8Span();
            var normalizer = new PathNormalizer(originalPath, PathNormalizer.Option.PreserveTailSeparator);

            Assert.Equal(Result.Success, normalizer.Result);

            // Compares addresses and lengths of the buffers
            Assert.True(originalPath.Value == normalizer.Path.Value);
        }

        [Fact]
        public static void Normalize_PreserveUncOptionOn_PreservesUncPath()
        {
            var normalizer = new PathNormalizer("//aa/bb/..".ToU8Span(), PathNormalizer.Option.PreserveUnc);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal(@"\\aa/bb", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_PreserveUncOptionOff_DoesNotPreserveUncPath()
        {
            var normalizer = new PathNormalizer("//aa/bb/..".ToU8Span(), PathNormalizer.Option.None);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal(@"/aa", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_MountNameOptionOn_ParsesMountName()
        {
            var normalizer = new PathNormalizer("mount:/a/./b".ToU8Span(), PathNormalizer.Option.HasMountName);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal("mount:/a/b", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_MountNameOptionOff_DoesNotParseMountName()
        {
            var normalizer = new PathNormalizer("mount:/a/./b".ToU8Span(), PathNormalizer.Option.None);

            Assert.Equal(ResultFs.InvalidPathFormat.Value, normalizer.Result);
        }
    }
}
