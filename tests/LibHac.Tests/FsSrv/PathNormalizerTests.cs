using LibHac.Common;
using LibHac.Fs;
using Xunit;
using PathNormalizer = LibHac.FsSrv.Impl.PathNormalizer;

namespace LibHac.Tests.FsSrv
{
    public class PathNormalizerTests
    {
        [Fact]
        public static void Ctor_EmptyPathWithAcceptEmptyOption_ReturnsEmptyPathWithSuccess()
        {
            using var normalizer = new PathNormalizer("".ToU8Span(), PathNormalizer.Option.AcceptEmpty);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.True(normalizer.Path.IsEmpty());
        }

        [Fact]
        public static void Normalize_PreserveTailSeparatorOption_KeepsExistingTailSeparator()
        {
            using var normalizer = new PathNormalizer("/a/./b/".ToU8Span(), PathNormalizer.Option.PreserveTrailingSeparator);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal("/a/b/", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_PreserveTailSeparatorOption_IgnoresMissingTailSeparator()
        {
            using var normalizer = new PathNormalizer("/a/./b".ToU8Span(), PathNormalizer.Option.PreserveTrailingSeparator);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal("/a/b", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_PathAlreadyNormalized_ReturnsSameBuffer()
        {
            var originalPath = "/a/b".ToU8Span();
            using var normalizer = new PathNormalizer(originalPath, PathNormalizer.Option.PreserveTrailingSeparator);

            Assert.Equal(Result.Success, normalizer.Result);

            // Compares addresses and lengths of the buffers
            Assert.True(originalPath.Value == normalizer.Path.Value);
        }

        [Fact]
        public static void Normalize_PreserveUncOptionOn_PreservesUncPath()
        {
            using var normalizer = new PathNormalizer("//aa/bb/..".ToU8Span(), PathNormalizer.Option.PreserveUnc);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal(@"\\aa/bb", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_PreserveUncOptionOff_DoesNotPreserveUncPath()
        {
            using var normalizer = new PathNormalizer("//aa/bb/..".ToU8Span(), PathNormalizer.Option.None);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal(@"/aa", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_MountNameOptionOn_ParsesMountName()
        {
            using var normalizer = new PathNormalizer("mount:/a/./b".ToU8Span(), PathNormalizer.Option.HasMountName);

            Assert.Equal(Result.Success, normalizer.Result);
            Assert.Equal("mount:/a/b", normalizer.Path.ToString());
        }

        [Fact]
        public static void Normalize_MountNameOptionOff_DoesNotParseMountName()
        {
            using var normalizer = new PathNormalizer("mount:/a/./b".ToU8Span(), PathNormalizer.Option.None);

            Assert.Equal(ResultFs.InvalidPathFormat.Value, normalizer.Result);
        }
    }
}
