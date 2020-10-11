using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Fs
{
    public class LayeredFileSystemTests
    {
        private IFileSystem CreateFileSystem()
        {
            var lowerLayerFs = new InMemoryFileSystem();
            var upperLayerFs = new InMemoryFileSystem();

            var layeredFs = new LayeredFileSystem(lowerLayerFs, upperLayerFs);

            lowerLayerFs.CreateDirectory("/dir".ToU8Span()).ThrowIfFailure();
            upperLayerFs.CreateDirectory("/dir".ToU8Span()).ThrowIfFailure();
            lowerLayerFs.CreateDirectory("/dir2".ToU8Span()).ThrowIfFailure();
            upperLayerFs.CreateDirectory("/dir2".ToU8Span()).ThrowIfFailure();
            lowerLayerFs.CreateDirectory("/dir3".ToU8Span()).ThrowIfFailure();
            upperLayerFs.CreateDirectory("/dir3".ToU8Span()).ThrowIfFailure();

            lowerLayerFs.CreateDirectory("/lowerDir".ToU8Span()).ThrowIfFailure();
            upperLayerFs.CreateDirectory("/upperDir".ToU8Span()).ThrowIfFailure();

            lowerLayerFs.CreateFile("/dir/replacedFile".ToU8Span(), 1, CreateFileOptions.None).ThrowIfFailure();
            upperLayerFs.CreateFile("/dir/replacedFile".ToU8Span(), 2, CreateFileOptions.None).ThrowIfFailure();

            lowerLayerFs.CreateFile("/dir2/lowerFile".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();
            upperLayerFs.CreateFile("/dir2/upperFile".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();

            lowerLayerFs.CreateFile("/dir3/lowerFile".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();
            upperLayerFs.CreateFile("/dir3/upperFile".ToU8Span(), 2, CreateFileOptions.None).ThrowIfFailure();
            lowerLayerFs.CreateFile("/dir3/replacedFile".ToU8Span(), 1, CreateFileOptions.None).ThrowIfFailure();
            upperLayerFs.CreateFile("/dir3/replacedFile".ToU8Span(), 2, CreateFileOptions.None).ThrowIfFailure();

            lowerLayerFs.CreateFile("/replacedWithDir".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();
            upperLayerFs.CreateDirectory("/replacedWithDir".ToU8Span()).ThrowIfFailure();
            upperLayerFs.CreateFile("/replacedWithDir/subFile".ToU8Span(), 0, CreateFileOptions.None).ThrowIfFailure();

            return layeredFs;
        }

        private IFileSystem CreateEmptyFileSystem()
        {
            var baseLayerFs = new InMemoryFileSystem();
            var topLayerFs = new InMemoryFileSystem();

            return new LayeredFileSystem(baseLayerFs, topLayerFs);
        }

        [Fact]
        public void OpenFile_FileDoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Result(ResultFs.PathNotFound, fs.OpenFile(out _, "/fakefile".ToU8Span(), OpenMode.All));
        }

        [Fact]
        public void OpenFile_FileIsInBothSources_ReturnsFileFromTopSource()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Success(fs.OpenFile(out IFile file, "/dir/replacedFile".ToU8Span(), OpenMode.All));
            Assert.Success(file.GetSize(out long fileSize));

            Assert.Equal(2, fileSize);
        }

        [Fact]
        public void OpenFile_InsideMergedDirectory_CanOpenFilesFromBothSources()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Success(fs.OpenFile(out _, "/dir2/lowerFile".ToU8Span(), OpenMode.All));
            Assert.Success(fs.OpenFile(out _, "/dir2/upperFile".ToU8Span(), OpenMode.All));
        }

        [Fact]
        public void OpenDirectory_DirDoesNotExist_ReturnsPathNotFound()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Result(ResultFs.PathNotFound, fs.OpenDirectory(out _, "/fakedir".ToU8Span(), OpenDirectoryMode.All));
        }

        [Fact]
        public void OpenDirectory_ExistsInSingleLayer_ReturnsNonMergedDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Success(fs.OpenDirectory(out IDirectory dir, "/lowerDir".ToU8Span(), OpenDirectoryMode.All));
            Assert.Equal(typeof(InMemoryFileSystem), dir.GetType().DeclaringType);
        }

        [Fact]
        public void OpenDirectory_ExistsInMultipleLayers_ReturnsMergedDirectory()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Success(fs.OpenDirectory(out IDirectory dir, "/dir".ToU8Span(), OpenDirectoryMode.All));
            Assert.Equal(typeof(LayeredFileSystem), dir.GetType().DeclaringType);
        }

        [Fact]
        public void GetEntryType_InsideMergedDirectory_CanGetEntryTypesFromBothSources()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Success(fs.GetEntryType(out _, "/dir2/lowerFile".ToU8Span()));
            Assert.Success(fs.GetEntryType(out _, "/dir2/upperFile".ToU8Span()));
        }

        [Fact]
        public void IDirectoryRead_DuplicatedEntriesAreReturnedOnlyOnce()
        {
            IFileSystem fs = CreateFileSystem();
            Span<DirectoryEntry> entries = stackalloc DirectoryEntry[4];

            Assert.Success(fs.OpenDirectory(out IDirectory directory, "/dir3".ToU8Span(), OpenDirectoryMode.All));

            Assert.Success(directory.Read(out long entriesRead, entries));
            Assert.Equal(3, entriesRead);
        }

        [Fact]
        public void IDirectoryRead_DuplicatedEntryReturnsFromTopLayer()
        {
            IFileSystem fs = CreateFileSystem();
            var entry = new DirectoryEntry();

            Assert.Success(fs.OpenDirectory(out IDirectory directory, "/dir".ToU8Span(), OpenDirectoryMode.All));

            Assert.Success(directory.Read(out _, SpanHelpers.AsSpan(ref entry)));
            Assert.Equal("replacedFile", StringUtils.Utf8ZToString(entry.Name));
            Assert.Equal(2, entry.Size);
        }

        [Fact]
        public void IDirectoryRead_EmptyFs_NoEntriesAreRead()
        {
            IFileSystem fs = CreateEmptyFileSystem();
            var entry = new DirectoryEntry();

            Assert.Success(fs.OpenDirectory(out IDirectory directory, "/".ToU8Span(), OpenDirectoryMode.All));

            Assert.Success(directory.Read(out long entriesRead, SpanHelpers.AsSpan(ref entry)));
            Assert.Equal(0, entriesRead);
        }

        [Fact]
        public void IDirectoryGetEntryCount_DuplicatedEntriesAreCountedOnlyOnce()
        {
            IFileSystem fs = CreateFileSystem();

            Assert.Success(fs.OpenDirectory(out IDirectory directory, "/dir3".ToU8Span(), OpenDirectoryMode.All));

            Assert.Success(directory.GetEntryCount(out long entryCount));
            Assert.Equal(3, entryCount);
        }

        [Fact]
        public void IDirectoryGetEntryCount_MergedDirectoryAfterRead_AllEntriesAreCounted()
        {
            IFileSystem fs = CreateFileSystem();
            var entry = new DirectoryEntry();

            Assert.Success(fs.OpenDirectory(out IDirectory directory, "/dir3".ToU8Span(), OpenDirectoryMode.All));

            // Read all entries
            long entriesRead;
            do
            {
                Assert.Success(directory.Read(out entriesRead, SpanHelpers.AsSpan(ref entry)));
            } while (entriesRead != 0);

            Assert.Success(directory.GetEntryCount(out long entryCount));
            Assert.Equal(3, entryCount);
        }

        [Fact]
        public void IDirectoryGetEntryCount_EmptyFs_EntryCountIsZero()
        {
            IFileSystem fs = CreateEmptyFileSystem();

            Assert.Success(fs.OpenDirectory(out IDirectory directory, "/".ToU8Span(), OpenDirectoryMode.All));

            Assert.Success(directory.GetEntryCount(out long entryCount));
            Assert.Equal(0, entryCount);
        }
    }
}
