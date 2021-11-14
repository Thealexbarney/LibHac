using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Util;
using Xunit;

namespace LibHac.Tests.Fs;

public class LayeredFileSystemTests
{
    private IFileSystem CreateFileSystem()
    {
        var lowerLayerFs = new InMemoryFileSystem();
        var upperLayerFs = new InMemoryFileSystem();

        var layeredFs = new LayeredFileSystem(lowerLayerFs, upperLayerFs);

        lowerLayerFs.CreateDirectory("/dir").ThrowIfFailure();
        upperLayerFs.CreateDirectory("/dir").ThrowIfFailure();
        lowerLayerFs.CreateDirectory("/dir2").ThrowIfFailure();
        upperLayerFs.CreateDirectory("/dir2").ThrowIfFailure();
        lowerLayerFs.CreateDirectory("/dir3").ThrowIfFailure();
        upperLayerFs.CreateDirectory("/dir3").ThrowIfFailure();

        lowerLayerFs.CreateDirectory("/lowerDir").ThrowIfFailure();
        upperLayerFs.CreateDirectory("/upperDir").ThrowIfFailure();

        lowerLayerFs.CreateFile("/dir/replacedFile", 1, CreateFileOptions.None).ThrowIfFailure();
        upperLayerFs.CreateFile("/dir/replacedFile", 2, CreateFileOptions.None).ThrowIfFailure();

        lowerLayerFs.CreateFile("/dir2/lowerFile", 0, CreateFileOptions.None).ThrowIfFailure();
        upperLayerFs.CreateFile("/dir2/upperFile", 0, CreateFileOptions.None).ThrowIfFailure();

        lowerLayerFs.CreateFile("/dir3/lowerFile", 0, CreateFileOptions.None).ThrowIfFailure();
        upperLayerFs.CreateFile("/dir3/upperFile", 2, CreateFileOptions.None).ThrowIfFailure();
        lowerLayerFs.CreateFile("/dir3/replacedFile", 1, CreateFileOptions.None).ThrowIfFailure();
        upperLayerFs.CreateFile("/dir3/replacedFile", 2, CreateFileOptions.None).ThrowIfFailure();

        lowerLayerFs.CreateFile("/replacedWithDir", 0, CreateFileOptions.None).ThrowIfFailure();
        upperLayerFs.CreateDirectory("/replacedWithDir").ThrowIfFailure();
        upperLayerFs.CreateFile("/replacedWithDir/subFile", 0, CreateFileOptions.None).ThrowIfFailure();

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

        using var file = new UniqueRef<IFile>();
        Assert.Result(ResultFs.PathNotFound, fs.OpenFile(ref file.Ref(), "/fakefile", OpenMode.All));
    }

    [Fact]
    public void OpenFile_FileIsInBothSources_ReturnsFileFromTopSource()
    {
        IFileSystem fs = CreateFileSystem();

        using var file = new UniqueRef<IFile>();
        Assert.Success(fs.OpenFile(ref file.Ref(), "/dir/replacedFile", OpenMode.All));
        Assert.Success(file.Get.GetSize(out long fileSize));

        Assert.Equal(2, fileSize);
    }

    [Fact]
    public void OpenFile_InsideMergedDirectory_CanOpenFilesFromBothSources()
    {
        IFileSystem fs = CreateFileSystem();

        using var file = new UniqueRef<IFile>();
        Assert.Success(fs.OpenFile(ref file.Ref(), "/dir2/lowerFile", OpenMode.All));
        Assert.Success(fs.OpenFile(ref file.Ref(), "/dir2/upperFile", OpenMode.All));
    }

    [Fact]
    public void OpenDirectory_DirDoesNotExist_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Result(ResultFs.PathNotFound, fs.OpenDirectory(ref directory.Ref(), "/fakedir", OpenDirectoryMode.All));
    }

    [Fact]
    public void OpenDirectory_ExistsInSingleLayer_ReturnsNonMergedDirectory()
    {
        IFileSystem fs = CreateFileSystem();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/lowerDir", OpenDirectoryMode.All));
        Assert.Equal(typeof(InMemoryFileSystem), directory.Get.GetType().DeclaringType);
    }

    [Fact]
    public void OpenDirectory_ExistsInMultipleLayers_ReturnsMergedDirectory()
    {
        IFileSystem fs = CreateFileSystem();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/dir", OpenDirectoryMode.All));
        Assert.Equal(typeof(LayeredFileSystem), directory.Get.GetType().DeclaringType);
    }

    [Fact]
    public void GetEntryType_InsideMergedDirectory_CanGetEntryTypesFromBothSources()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.GetEntryType(out _, "/dir2/lowerFile"));
        Assert.Success(fs.GetEntryType(out _, "/dir2/upperFile"));
    }

    [Fact]
    public void IDirectoryRead_DuplicatedEntriesAreReturnedOnlyOnce()
    {
        IFileSystem fs = CreateFileSystem();
        Span<DirectoryEntry> entries = stackalloc DirectoryEntry[4];

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/dir3", OpenDirectoryMode.All));

        Assert.Success(directory.Get.Read(out long entriesRead, entries));
        Assert.Equal(3, entriesRead);
    }

    [Fact]
    public void IDirectoryRead_DuplicatedEntryReturnsFromTopLayer()
    {
        IFileSystem fs = CreateFileSystem();
        var entry = new DirectoryEntry();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/dir", OpenDirectoryMode.All));

        Assert.Success(directory.Get.Read(out _, SpanHelpers.AsSpan(ref entry)));
        Assert.Equal("replacedFile", StringUtils.Utf8ZToString(entry.Name));
        Assert.Equal(2, entry.Size);
    }

    [Fact]
    public void IDirectoryRead_EmptyFs_NoEntriesAreRead()
    {
        IFileSystem fs = CreateEmptyFileSystem();
        var entry = new DirectoryEntry();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/", OpenDirectoryMode.All));

        Assert.Success(directory.Get.Read(out long entriesRead, SpanHelpers.AsSpan(ref entry)));
        Assert.Equal(0, entriesRead);
    }

    [Fact]
    public void IDirectoryGetEntryCount_DuplicatedEntriesAreCountedOnlyOnce()
    {
        IFileSystem fs = CreateFileSystem();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/dir3", OpenDirectoryMode.All));

        Assert.Success(directory.Get.GetEntryCount(out long entryCount));
        Assert.Equal(3, entryCount);
    }

    [Fact]
    public void IDirectoryGetEntryCount_MergedDirectoryAfterRead_AllEntriesAreCounted()
    {
        IFileSystem fs = CreateFileSystem();
        var entry = new DirectoryEntry();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/dir3", OpenDirectoryMode.All));

        // Read all entries
        long entriesRead;
        do
        {
            Assert.Success(directory.Get.Read(out entriesRead, SpanHelpers.AsSpan(ref entry)));
        } while (entriesRead != 0);

        Assert.Success(directory.Get.GetEntryCount(out long entryCount));
        Assert.Equal(3, entryCount);
    }

    [Fact]
    public void IDirectoryGetEntryCount_EmptyFs_EntryCountIsZero()
    {
        IFileSystem fs = CreateEmptyFileSystem();

        using var directory = new UniqueRef<IDirectory>();
        Assert.Success(fs.OpenDirectory(ref directory.Ref(), "/", OpenDirectoryMode.All));

        Assert.Success(directory.Get.GetEntryCount(out long entryCount));
        Assert.Equal(0, entryCount);
    }
}
