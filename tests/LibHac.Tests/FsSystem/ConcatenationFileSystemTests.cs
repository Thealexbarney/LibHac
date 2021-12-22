using System;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;
using LibHac.Tests.Fs;
using LibHac.Tests.Fs.IFileSystemTestBase;
using LibHac.Tools.Fs;
using Xunit;

namespace LibHac.Tests.FsSystem;

public class ConcatenationFileSystemTests : IFileSystemTests
{
    private const int InternalFileSize = 0x10000;
    protected override IFileSystem CreateFileSystem()
    {
        return CreateFileSystemInternal().concatFs;
    }

    private (InMemoryFileSystem baseFs, ConcatenationFileSystem concatFs) CreateFileSystemInternal()
    {
        var baseFs = new InMemoryFileSystem();

        using var uniqueBaseFs = new UniqueRef<IAttributeFileSystem>(baseFs);
        var concatFs = new ConcatenationFileSystem(ref uniqueBaseFs.Ref(), InternalFileSize);

        return (baseFs, concatFs);
    }

    [Fact]
    public void OpenFile_OpenInternalFile_OpensSuccessfully()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", InternalFileSize * 3, CreateFileOptions.CreateConcatenationFile));

        using var file = new UniqueRef<IFile>();
        Assert.Success(fs.OpenFile(ref file.Ref(), "/file/01", OpenMode.All));
    }

    [Fact]
    public void OpenFile_ConcatFileWithNoInternalFiles_ReturnsConcatenationFsInvalidInternalFileCount()
    {
        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        Assert.Success(concatFs.CreateFile("/file", InternalFileSize * 3, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(baseFs.DeleteFile("/file/00"));

        using var file = new UniqueRef<IFile>();
        Assert.Result(ResultFs.ConcatenationFsInvalidInternalFileCount, concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
    }

    [Fact]
    public void OpenDirectory_OpenConcatFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", InternalFileSize * 3, CreateFileOptions.CreateConcatenationFile));

        using var dir = new UniqueRef<IDirectory>();
        Assert.Result(ResultFs.PathNotFound, fs.OpenDirectory(ref dir.Ref(), "/file", OpenDirectoryMode.All));
    }

    [Fact]
    public void CreateFile_ConcatenationFile_GetEntryTypeReturnsFile()
    {
        IFileSystem concatFs = CreateFileSystem();

        Assert.Success(concatFs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));

        Assert.Success(concatFs.GetEntryType(out DirectoryEntryType type, "/file"));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void CreateFile_EmptyConcatenationFile_BaseDirHasCorrectStructure()
    {
        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        Assert.Success(concatFs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/01"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Equal(0, internalFile00Size);
    }

    [Fact]
    public void CreateFile_SizeIsMultipleOfInternalFile_BaseDirHasCorrectStructure()
    {
        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        Assert.Success(concatFs.CreateFile("/file", InternalFileSize * 2, CreateFileOptions.CreateConcatenationFile));

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Success(baseFs.GetFileSize(out long internalFile01Size, "/file/01"));
        Assert.Equal(InternalFileSize, internalFile00Size);
        Assert.Equal(InternalFileSize, internalFile01Size);
    }

    [Fact]
    public void CreateFile_NormalFileInsideConcatFile_CreatesSuccessfully()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(fs.CreateFile("/file/file", 0));

        Assert.Success(fs.GetEntryType(out DirectoryEntryType type, "/file/file"));
        Assert.Equal(DirectoryEntryType.File, type);
    }

    [Fact]
    public void CreateFile_ConcatFileInsideConcatFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));

        Assert.Result(ResultFs.PathNotFound, fs.CreateFile("/file/file", 0, CreateFileOptions.CreateConcatenationFile));
    }

    [Fact]
    public void DeleteFile_DeleteConcatFile_DeletesSuccessfully()
    {
        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        Assert.Success(concatFs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.DeleteFile("/file"));

        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file"));
    }

    [Fact]
    public void CreateDirectory_InsideConcatFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));

        Assert.Result(ResultFs.PathNotFound, fs.CreateDirectory("/file/dir"));
    }

    [Fact]
    public void DeleteDirectory_DeleteConcatFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));

        Assert.Result(ResultFs.PathNotFound, fs.DeleteDirectory("/file"));
    }

    [Fact]
    public void CleanDirectoryRecursively_CleanConcatFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));

        Assert.Result(ResultFs.PathNotFound, fs.CleanDirectoryRecursively("/file"));
    }

    [Fact]
    public void RenameFile_RenameConcatFile_RenamesSuccessfully()
    {
        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        Assert.Success(concatFs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.RenameFile("/file", "/file2"));

        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/file2"));
        Assert.Equal(DirectoryEntryType.Directory, type);
    }

    [Fact]
    public void RenameDirectory_RenameConcatFile_ReturnsPathNotFound()
    {
        IFileSystem fs = CreateFileSystem();

        Assert.Success(fs.CreateFile("/file", 0, CreateFileOptions.CreateConcatenationFile));

        Assert.Result(ResultFs.PathNotFound, fs.RenameDirectory("/file", "/file2"));
    }

    [Fact]
    public void Write_ConcatFileWithMultipleInternalFiles_CanReadBackWrittenData()
    {
        const long fileSize = InternalFileSize * 5 - 5;

        byte[] data = new byte[fileSize];
        new Random(1234).NextBytes(data);

        IFileSystem fs = CreateFileSystem();

        fs.CreateFile("/file", fileSize, CreateFileOptions.CreateConcatenationFile);

        using var file = new UniqueRef<IFile>();
        fs.OpenFile(ref file.Ref(), "/file", OpenMode.Write);
        file.Get.Write(0, data, WriteOption.None);
        file.Reset();

        byte[] readData = new byte[data.Length];
        fs.OpenFile(ref file.Ref(), "/file", OpenMode.Read);

        Assert.Success(file.Get.Read(out long bytesRead, 0, readData, ReadOption.None));
        Assert.Equal(data.Length, bytesRead);

        Assert.Equal(data, readData);
    }

    [Fact]
    public void SetSize_ResizeToHigherMultipleOfInternalFile_BaseDirHasCorrectStructure()
    {
        const long originalSize = InternalFileSize;
        const long newSize = InternalFileSize * 2;

        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        using var file = new UniqueRef<IFile>();

        // Create the file and then resize it
        Assert.Success(concatFs.CreateFile("/file", originalSize, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
        Assert.Success(file.Get.SetSize(newSize));

        Assert.Success(file.Get.GetSize(out long concatFileSize));
        Assert.Equal(newSize, concatFileSize);

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile01Type, "/file/01"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);
        Assert.Equal(DirectoryEntryType.File, internalFile01Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Success(baseFs.GetFileSize(out long internalFile01Size, "/file/01"));
        Assert.Equal(InternalFileSize, internalFile00Size);
        Assert.Equal(InternalFileSize, internalFile01Size);
    }

    [Fact]
    public void SetSize_ResizeToLowerMultipleOfInternalFile_BaseDirHasCorrectStructure()
    {
        const long originalSize = InternalFileSize * 5;
        const long newSize = InternalFileSize * 2;

        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        using var file = new UniqueRef<IFile>();

        // Create the file and then resize it
        Assert.Success(concatFs.CreateFile("/file", originalSize, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
        Assert.Success(file.Get.SetSize(newSize));

        Assert.Success(file.Get.GetSize(out long concatFileSize));
        Assert.Equal(newSize, concatFileSize);

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile01Type, "/file/01"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);
        Assert.Equal(DirectoryEntryType.File, internalFile01Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/03"));
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/04"));
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/05"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Success(baseFs.GetFileSize(out long internalFile01Size, "/file/01"));
        Assert.Equal(InternalFileSize, internalFile00Size);
        Assert.Equal(InternalFileSize, internalFile01Size);
    }

    [Fact]
    public void SetSize_ResizeSmallerWithoutChangingInternalFileCount_BaseDirHasCorrectStructure()
    {
        const long originalSize = InternalFileSize * 2;
        const long newSize = InternalFileSize * 2 - 5;

        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        using var file = new UniqueRef<IFile>();

        // Create the file and then resize it
        Assert.Success(concatFs.CreateFile("/file", originalSize, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
        Assert.Success(file.Get.SetSize(newSize));

        Assert.Success(file.Get.GetSize(out long concatFileSize));
        Assert.Equal(newSize, concatFileSize);

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile01Type, "/file/01"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);
        Assert.Equal(DirectoryEntryType.File, internalFile01Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Success(baseFs.GetFileSize(out long internalFile01Size, "/file/01"));
        Assert.Equal(InternalFileSize, internalFile00Size);
        Assert.Equal(InternalFileSize - 5, internalFile01Size);
    }

    [Fact]
    public void SetSize_ResizeLargerWithoutChangingInternalFileCount_BaseDirHasCorrectStructure()
    {
        const long originalSize = InternalFileSize + 5;
        const long newSize = InternalFileSize * 2 - 5;

        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        using var file = new UniqueRef<IFile>();

        // Create the file and then resize it
        Assert.Success(concatFs.CreateFile("/file", originalSize, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
        Assert.Success(file.Get.SetSize(newSize));

        Assert.Success(file.Get.GetSize(out long concatFileSize));
        Assert.Equal(newSize, concatFileSize);

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile01Type, "/file/01"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);
        Assert.Equal(DirectoryEntryType.File, internalFile01Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Success(baseFs.GetFileSize(out long internalFile01Size, "/file/01"));
        Assert.Equal(InternalFileSize, internalFile00Size);
        Assert.Equal(InternalFileSize - 5, internalFile01Size);
    }

    [Fact]
    public void SetSize_ResizeSmallerChangingInternalFileCount_BaseDirHasCorrectStructure()
    {
        const long originalSize = InternalFileSize * 4 + 5;
        const long newSize = InternalFileSize * 2 - 5;

        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        using var file = new UniqueRef<IFile>();

        // Create the file and then resize it
        Assert.Success(concatFs.CreateFile("/file", originalSize, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
        Assert.Success(file.Get.SetSize(newSize));

        Assert.Success(file.Get.GetSize(out long concatFileSize));
        Assert.Equal(newSize, concatFileSize);

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile01Type, "/file/01"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);
        Assert.Equal(DirectoryEntryType.File, internalFile01Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/03"));
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/04"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Success(baseFs.GetFileSize(out long internalFile01Size, "/file/01"));
        Assert.Equal(InternalFileSize, internalFile00Size);
        Assert.Equal(InternalFileSize - 5, internalFile01Size);
    }

    [Fact]
    public void SetSize_ResizeLargerChangingInternalFileCount_BaseDirHasCorrectStructure()
    {
        const long originalSize = InternalFileSize - 5;
        const long newSize = InternalFileSize * 2 - 5;

        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        using var file = new UniqueRef<IFile>();

        // Create the file and then resize it
        Assert.Success(concatFs.CreateFile("/file", originalSize, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
        Assert.Success(file.Get.SetSize(newSize));

        Assert.Success(file.Get.GetSize(out long concatFileSize));
        Assert.Equal(newSize, concatFileSize);

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile01Type, "/file/01"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);
        Assert.Equal(DirectoryEntryType.File, internalFile01Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Success(baseFs.GetFileSize(out long internalFile01Size, "/file/01"));
        Assert.Equal(InternalFileSize, internalFile00Size);
        Assert.Equal(InternalFileSize - 5, internalFile01Size);
    }

    [Fact]
    public void SetSize_ResizeToEmpty_BaseDirHasCorrectStructure()
    {
        const long originalSize = InternalFileSize * 2 + 5;
        const long newSize = 0;

        (IAttributeFileSystem baseFs, IFileSystem concatFs) = CreateFileSystemInternal();

        using var file = new UniqueRef<IFile>();

        // Create the file and then resize it
        Assert.Success(concatFs.CreateFile("/file", originalSize, CreateFileOptions.CreateConcatenationFile));
        Assert.Success(concatFs.OpenFile(ref file.Ref(), "/file", OpenMode.All));
        Assert.Success(file.Get.SetSize(newSize));

        Assert.Success(file.Get.GetSize(out long concatFileSize));
        Assert.Equal(newSize, concatFileSize);

        // Ensure the directory exists with the archive bit set
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType dirType, "/file"));
        Assert.Success(baseFs.GetFileAttributes(out NxFileAttributes dirAttributes, "/file"));
        Assert.Equal(DirectoryEntryType.Directory, dirType);
        Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, dirAttributes);

        // Ensure the internal files exist
        Assert.Success(baseFs.GetEntryType(out DirectoryEntryType internalFile00Type, "/file/00"));
        Assert.Equal(DirectoryEntryType.File, internalFile00Type);

        // Ensure no additional internal files exist
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/01"));
        Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/file/02"));

        // Ensure the internal file sizes are correct
        Assert.Success(baseFs.GetFileSize(out long internalFile00Size, "/file/00"));
        Assert.Equal(0, internalFile00Size);
    }

    [Fact]
    public void SetSize_ResizeToHigherMultipleOfInternalFile_FileContentsAreRetained()
    {
        EnsureContentsAreRetainedOnResize(InternalFileSize, InternalFileSize * 2);
    }

    [Fact]
    public void SetSize_ResizeToLowerMultipleOfInternalFile_FileContentsAreRetained()
    {
        EnsureContentsAreRetainedOnResize(InternalFileSize * 5, InternalFileSize * 2);
    }

    [Fact]
    public void SetSize__ResizeSmallerWithoutChangingInternalFileCount_FileContentsAreRetained()
    {
        EnsureContentsAreRetainedOnResize(InternalFileSize * 2, InternalFileSize * 2 - 5);
    }

    [Fact]
    public void SetSize_ResizeLargerWithoutChangingInternalFileCount_FileContentsAreRetained()
    {
        EnsureContentsAreRetainedOnResize(InternalFileSize + 5, InternalFileSize * 2 - 5);
    }

    [Fact]
    public void SetSize_ResizeSmallerChangingInternalFileCount_FileContentsAreRetained()
    {
        EnsureContentsAreRetainedOnResize(InternalFileSize * 4 + 5, InternalFileSize * 2 - 5);
    }

    [Fact]
    public void SetSize_ResizeLargerChangingInternalFileCount_FileContentsAreRetained()
    {
        EnsureContentsAreRetainedOnResize(InternalFileSize - 5, InternalFileSize * 2 - 5);
    }

    private void EnsureContentsAreRetainedOnResize(int originalSize, int newSize)
    {
        const string fileName = "/file";

        byte[] originalData = new byte[originalSize];
        new Random(1234).NextBytes(originalData);

        byte[] newData = new byte[newSize];
        byte[] actualNewData = new byte[newSize];
        originalData.AsSpan(0, Math.Min(originalSize, newSize)).CopyTo(newData);

        IFileSystem fs = CreateFileSystem();

        // Create the file and write the data to it
        using (var file = new UniqueRef<IFile>())
        {
            // Create the file and then write the data to it
            Assert.Success(fs.CreateFile(fileName, originalSize, CreateFileOptions.CreateConcatenationFile));
            Assert.Success(fs.OpenFile(ref file.Ref(), fileName, OpenMode.Write));
            Assert.Success(file.Get.Write(0, originalData, WriteOption.None));
        }

        // Resize the file
        using (var file = new UniqueRef<IFile>())
        {
            Assert.Success(fs.OpenFile(ref file.Ref(), fileName, OpenMode.Write));
            Assert.Success(file.Get.SetSize(newSize));
        }

        // Read back the entire resized file and ensure the contents are as expected
        using (var file = new UniqueRef<IFile>())
        {
            Assert.Success(fs.OpenFile(ref file.Ref(), fileName, OpenMode.Read));
            Assert.Success(file.Get.Read(out long bytesRead, 0, actualNewData));
            Assert.Equal(newSize, bytesRead);
        }

        Assert.Equal(newData, actualNewData);
    }
}