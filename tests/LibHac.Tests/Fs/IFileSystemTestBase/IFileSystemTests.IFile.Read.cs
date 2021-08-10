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
        public void IFileRead_BytesReadContainsNumberOfBytesRead()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 100, CreateFileOptions.None);

            byte[] buffer = new byte[20];
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Read);

            Assert.Success(file.Get.Read(out long bytesRead, 50, buffer, ReadOption.None));
            Assert.Equal(20, bytesRead);
        }

        [Fact]
        public void IFileRead_OffsetPastEndOfFile_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Read);

            Result rc = file.Get.Read(out _, 1, buffer, ReadOption.None);
            Assert.Result(ResultFs.OutOfRange, rc);
        }

        [Fact]
        public void IFileRead_OpenModeNoRead_ReturnsInvalidOpenModeForRead()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Write);

            Result rc = file.Get.Read(out _, 0, buffer, ReadOption.None);
            Assert.Result(ResultFs.ReadUnpermitted, rc);
        }

        [Fact]
        public void IFileRead_NegativeOffset_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Write);

            Result rc = file.Get.Read(out _, -5, buffer, ReadOption.None);
            Assert.Result(ResultFs.OutOfRange, rc);
        }

        [Fact]
        public void IFileRead_OffsetPlusSizeOverflows_ReturnsOutOfRange()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            byte[] buffer = new byte[10];
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Write);

            Result rc = file.Get.Read(out _, long.MaxValue - 5, buffer, ReadOption.None);
            Assert.Result(ResultFs.OutOfRange, rc);
        }

        [Fact]
        public void IFileRead_FileTooSmallToFillBuffer_BytesReadContainsAvailableByteCount()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 100, CreateFileOptions.None);

            byte[] buffer = new byte[200];
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Read);

            Assert.Success(file.Get.Read(out long bytesRead, 90, buffer, ReadOption.None));
            Assert.Equal(10, bytesRead);
        }

        [Fact]
        public void IFileRead_FileTooSmallToFillBuffer_DoesPartialRead()
        {
            IFileSystem fs = CreateFileSystem();

            fs.CreateFile("/file", 100, CreateFileOptions.None);

            // The contents of a created file are undefined, so zero the file
            using var file = new UniqueRef<IFile>();
            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Write);
            file.Get.Write(0, new byte[100], WriteOption.None);
            file.Reset();

            byte[] bufferExpected = new byte[200];
            bufferExpected.AsSpan(10).Fill(0xCC);

            byte[] buffer = new byte[200];
            buffer.AsSpan().Fill(0xCC);

            fs.OpenFile(ref file.Ref(), "/file", OpenMode.Read);

            Assert.Success(file.Get.Read(out _, 90, buffer, ReadOption.None));
            Assert.Equal(bufferExpected, buffer);
        }
    }
}