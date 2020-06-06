using System;
using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void IFileRead_BytesReadContainsNumberOfBytesRead()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 100, CreateFileOptions.None);

            var buffer = new byte[20];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Assert.True(file.Read(out long bytesRead, 50, buffer, ReadOptionFlag.None).IsSuccess());
                Assert.Equal(20, bytesRead);
            }
        }

        [Fact]
        public void IFileRead_OffsetPastEndOfFile_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Result rc = file.Read(out _, 1, buffer, ReadOptionFlag.None);
                Assert.Equal(ResultFs.OutOfRange.Value, rc);
            }
        }

        [Fact]
        public void IFileRead_OpenModeNoRead_ReturnsInvalidOpenModeForRead()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            using (file)
            {
                Result rc = file.Read(out _, 0, buffer, ReadOptionFlag.None);
                Assert.Equal(ResultFs.InvalidOpenModeForRead.Value, rc);
            }
        }

        [Fact]
        public void IFileRead_NegativeOffset_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            using (file)
            {
                Result rc = file.Read(out _, -5, buffer, ReadOptionFlag.None);
                Assert.Equal(ResultFs.OutOfRange.Value, rc);
            }
        }

        [Fact]
        public void IFileRead_OffsetPlusSizeOverflows_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 0, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            using (file)
            {
                Result rc = file.Read(out _, long.MaxValue - 5, buffer, ReadOptionFlag.None);
                Assert.Equal(ResultFs.OutOfRange.Value, rc);
            }
        }

        [Fact]
        public void IFileRead_FileTooSmallToFillBuffer_BytesReadContainsAvailableByteCount()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 100, CreateFileOptions.None);

            var buffer = new byte[200];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Assert.True(file.Read(out long bytesRead, 90, buffer, ReadOptionFlag.None).IsSuccess());
                Assert.Equal(10, bytesRead);
            }
        }

        [Fact]
        public void IFileRead_FileTooSmallToFillBuffer_DoesPartialRead()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 100, CreateFileOptions.None);

            // The contents of a created file are undefined, so zero the file
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            using (file)
            {
                file.Write(0, new byte[100], WriteOptionFlag.None);
            }

            var bufferExpected = new byte[200];
            bufferExpected.AsSpan(10).Fill(0xCC);

            var buffer = new byte[200];
            buffer.AsSpan().Fill(0xCC);

            fs.OpenFile(out file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Assert.True(file.Read(out _, 90, buffer, ReadOptionFlag.None).IsSuccess());
                Assert.Equal(bufferExpected, buffer);
            }
        }
    }
}