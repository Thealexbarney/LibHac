using System.Runtime.CompilerServices;
using LibHac.Common.FixedArrays;
using Xunit;

namespace LibHac.Tests.Common
{
    public class FixedArraySizeTests
    {
        [Fact] public static void Array1SpanSizeIsCorrect() => Assert.Equal(1, new Array1<byte>().Items.Length);
        [Fact] public static void Array1ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 1, Unsafe.SizeOf<Array1<byte>>());
        [Fact] public static void Array1LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 1, Unsafe.SizeOf<Array1<long>>());

        [Fact] public static void Array3SpanSizeIsCorrect() => Assert.Equal(3, new Array3<byte>().Items.Length);
        [Fact] public static void Array3ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 3, Unsafe.SizeOf<Array3<byte>>());
        [Fact] public static void Array3LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 3, Unsafe.SizeOf<Array3<long>>());

        [Fact] public static void Array8SpanSizeIsCorrect() => Assert.Equal(8, new Array8<byte>().Items.Length);
        [Fact] public static void Array8ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 8, Unsafe.SizeOf<Array8<byte>>());
        [Fact] public static void Array8LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 8, Unsafe.SizeOf<Array8<long>>());

        [Fact] public static void Array12SpanSizeIsCorrect() => Assert.Equal(12, new Array12<byte>().Items.Length);
        [Fact] public static void Array12ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 12, Unsafe.SizeOf<Array12<byte>>());
        [Fact] public static void Array12LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 12, Unsafe.SizeOf<Array12<long>>());

        [Fact] public static void Array32SpanSizeIsCorrect() => Assert.Equal(32, new Array32<byte>().Items.Length);
        [Fact] public static void Array32ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 32, Unsafe.SizeOf<Array32<byte>>());
        [Fact] public static void Array32LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 32, Unsafe.SizeOf<Array32<long>>());

        [Fact] public static void Array64SpanSizeIsCorrect() => Assert.Equal(64, new Array64<byte>().Items.Length);
        [Fact] public static void Array64ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 64, Unsafe.SizeOf<Array64<byte>>());
        [Fact] public static void Array64LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 64, Unsafe.SizeOf<Array64<long>>());

        [Fact] public static void Array128SpanSizeIsCorrect() => Assert.Equal(128, new Array128<byte>().Items.Length);
        [Fact] public static void Array128ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 128, Unsafe.SizeOf<Array128<byte>>());
        [Fact] public static void Array128LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 128, Unsafe.SizeOf<Array128<long>>());

        [Fact] public static void Array256SpanSizeIsCorrect() => Assert.Equal(256, new Array256<byte>().Items.Length);
        [Fact] public static void Array256ByteSizeIsCorrect() => Assert.Equal(sizeof(byte) * 256, Unsafe.SizeOf<Array256<byte>>());
        [Fact] public static void Array256LongSizeIsCorrect() => Assert.Equal(sizeof(long) * 256, Unsafe.SizeOf<Array256<long>>());
    }
}
