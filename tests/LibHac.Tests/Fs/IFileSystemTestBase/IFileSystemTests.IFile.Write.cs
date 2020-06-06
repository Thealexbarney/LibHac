using System;
using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void IFileWrite_CanReadBackWrittenData()
        {
            var data = new byte[] { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };

            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), data.Length, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            file.Write(0, data, WriteOptionFlag.None);
            file.Dispose();

            var readData = new byte[data.Length];

            fs.OpenFile(out file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Assert.True(file.Read(out long bytesRead, 0, readData, ReadOptionFlag.None).IsSuccess());
                Assert.Equal(data.Length, bytesRead);
            }

            Assert.Equal(data, readData);
        }

        [Fact]
        public void IFileWrite_WritePastEndOfFileWithNoAppend_ReturnsFileExtensionWithoutOpenModeAllowAppend()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            using (file)
            {
                Result rc = file.Write(5, buffer, WriteOptionFlag.None);
                Assert.Equal(ResultFs.FileExtensionWithoutOpenModeAllowAppend.Value, rc);
            }
        }

        [Fact]
        public void IFileWrite_OpenModeNoWrite_ReturnsInvalidOpenModeForWrite()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Result rc = file.Write(5, buffer, WriteOptionFlag.None);
                Assert.Equal(ResultFs.InvalidOpenModeForWrite.Value, rc);
            }
        }

        [Fact]
        public void IFileWrite_NegativeOffset_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Result rc = file.Write(-5, buffer, WriteOptionFlag.None);
                Assert.Equal(ResultFs.OutOfRange.Value, rc);
            }
        }

        [Fact]
        public void IFileWrite_OffsetPlusSizeOverflows_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Result rc = file.Write(long.MaxValue - 5, buffer, WriteOptionFlag.None);
                Assert.Equal(ResultFs.OutOfRange.Value, rc);
            }
        }

        [Fact]
        public void IFileWrite_WritePartiallyPastEndOfFileAppendAllowed_FileIsExtended()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.All);
            using (file)
            {
                Assert.True(file.Write(5, buffer, WriteOptionFlag.None).IsSuccess());

                file.GetSize(out long newSize);
                Assert.Equal(15, newSize);
            }
        }

        [Fact]
        public void IFileWrite_WritePastEndOfFileAppendAllowed_FileIsExtended()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            var buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.All);
            using (file)
            {
                Assert.True(file.Write(15, buffer, WriteOptionFlag.None).IsSuccess());

                file.GetSize(out long newSize);
                Assert.Equal(25, newSize);
            }
        }

        [Fact]
        public void IFileWrite_WritePastEndOfFileAppendAllowed_DataIsWritten()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            var bufferExpected = new byte[25];
            bufferExpected.AsSpan(15).Fill(0xCC);

            var writeBuffer = new byte[10];
            writeBuffer.AsSpan().Fill(0xCC);

            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.All);
            using (file)
            {
                Assert.True(file.Write(15, writeBuffer, WriteOptionFlag.None).IsSuccess());

                // Unwritten portions of new files are undefined, so write to the other portions
                file.Write(0, new byte[15], WriteOptionFlag.None);
            }

            var readBuffer = new byte[25];

            fs.OpenFile(out file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                file.Read(out _, 0, readBuffer, ReadOptionFlag.None);
                Assert.Equal(bufferExpected, readBuffer);
            }
        }
    }
}