using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract partial class IFileSystemTests
    {
        [Fact]
        public void IFileWrite_CanReadBackWrittenData()
        {
            byte[] data = { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };

            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), data.Length, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            file.Write(0, data, WriteOption.None);
            file.Dispose();

            byte[] readData = new byte[data.Length];

            fs.OpenFile(out file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Assert.Success(file.Read(out long bytesRead, 0, readData, ReadOption.None));
                Assert.Equal(data.Length, bytesRead);
            }

            Assert.Equal(data, readData);
        }

        [Fact]
        public void IFileWrite_WritePastEndOfFileWithNoAppend_ReturnsFileExtensionWithoutOpenModeAllowAppend()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Write);
            using (file)
            {
                Result rc = file.Write(5, buffer, WriteOption.None);
                Assert.Result(ResultFs.FileExtensionWithoutOpenModeAllowAppend, rc);
            }
        }

        [Fact]
        public void IFileWrite_OpenModeNoWrite_ReturnsInvalidOpenModeForWrite()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Result rc = file.Write(5, buffer, WriteOption.None);
                Assert.Result(ResultFs.WriteUnpermitted, rc);
            }
        }

        [Fact]
        public void IFileWrite_NegativeOffset_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Result rc = file.Write(-5, buffer, WriteOption.None);
                Assert.Result(ResultFs.OutOfRange, rc);
            }
        }

        [Fact]
        public void IFileWrite_OffsetPlusSizeOverflows_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                Result rc = file.Write(long.MaxValue - 5, buffer, WriteOption.None);
                Assert.Result(ResultFs.OutOfRange, rc);
            }
        }

        [Fact]
        public void IFileWrite_WritePartiallyPastEndOfFileAppendAllowed_FileIsExtended()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.All);
            using (file)
            {
                Assert.Success(file.Write(5, buffer, WriteOption.None));

                file.GetSize(out long newSize);
                Assert.Equal(15, newSize);
            }
        }

        [Fact]
        public void IFileWrite_WritePastEndOfFileAppendAllowed_FileIsExtended()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.All);
            using (file)
            {
                Assert.Success(file.Write(15, buffer, WriteOption.None));

                file.GetSize(out long newSize);
                Assert.Equal(25, newSize);
            }
        }

        [Fact]
        public void IFileWrite_WritePastEndOfFileAppendAllowed_DataIsWritten()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file".ToU8Span(), 10, CreateFileOptions.None);

            byte[] bufferExpected = new byte[25];
            bufferExpected.AsSpan(15).Fill(0xCC);

            byte[] writeBuffer = new byte[10];
            writeBuffer.AsSpan().Fill(0xCC);

            fs.OpenFile(out IFile file, "/file".ToU8Span(), OpenMode.All);
            using (file)
            {
                Assert.Success(file.Write(15, writeBuffer, WriteOption.None));

                // Unwritten portions of new files are undefined, so write to the other portions
                file.Write(0, new byte[15], WriteOption.None);
            }

            byte[] readBuffer = new byte[25];

            fs.OpenFile(out file, "/file".ToU8Span(), OpenMode.Read);
            using (file)
            {
                file.Read(out _, 0, readBuffer, ReadOption.None);
                Assert.Equal(bufferExpected, readBuffer);
            }
        }
    }
}