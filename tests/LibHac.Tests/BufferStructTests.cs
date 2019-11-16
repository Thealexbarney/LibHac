using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using Xunit;

namespace LibHac.Tests
{
    public class BufferStructTests
    {
        [Fact]
        public static void BufferIndexer()
        {
            var buffer = new Buffer16();

            buffer[0] = 5;
            buffer[1] = 6;

            Assert.Equal(5, buffer[0]);
            Assert.Equal(6, buffer[1]);
        }

        [Fact]
        public static void CastBufferToByteSpan()
        {
            var buffer = new Buffer16();

            Span<byte> byteSpan = buffer.Bytes;

            Assert.Equal(16, byteSpan.Length);
            Assert.True(Unsafe.AreSame(ref Unsafe.As<Buffer16, byte>(ref buffer), ref byteSpan[0]));
        }

        [Fact]
        public static void CastBufferToByteSpanImplicit()
        {
            var buffer = new Buffer16();

            Span<byte> byteSpan = buffer;

            Assert.Equal(16, byteSpan.Length);
            Assert.True(Unsafe.AreSame(ref Unsafe.As<Buffer16, byte>(ref buffer), ref byteSpan[0]));
        }

        [Fact]
        public static void CastBufferToReadOnlyByteSpanImplicit()
        {
            var buffer = new Buffer16();

            ReadOnlySpan<byte> byteSpan = buffer;

            Assert.Equal(16, byteSpan.Length);
            Assert.True(Unsafe.AreSame(ref Unsafe.As<Buffer16, byte>(ref buffer), ref Unsafe.AsRef(byteSpan[0])));
        }

        [Fact]
        public static void CastBufferToSpan()
        {
            var buffer = new Buffer16();

            Span<ulong> ulongSpan = buffer.AsSpan<ulong>();

            Assert.Equal(2, ulongSpan.Length);
            Assert.True(Unsafe.AreSame(ref Unsafe.As<Buffer16, ulong>(ref buffer), ref ulongSpan[0]));
        }

        [Fact]
        public static void CastBufferToStruct()
        {
            var buffer = new Buffer16();

            ref ulong ulongSpan = ref buffer.As<ulong>();

            Assert.True(Unsafe.AreSame(ref Unsafe.As<Buffer16, ulong>(ref buffer), ref ulongSpan));
        }

        [Fact]
        public static void CastBufferToLargerStruct()
        {
            var buffer = new Buffer16();

            Assert.Throws<ArgumentException>(() => buffer.As<Struct32Bytes>());
        }

        [StructLayout(LayoutKind.Sequential, Size = 32)]
        private struct Struct32Bytes { }
    }
}
