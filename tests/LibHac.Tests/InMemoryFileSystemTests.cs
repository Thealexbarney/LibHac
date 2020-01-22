using System;
using LibHac.Common;
using LibHac.Fs;
using Xunit;

namespace LibHac.Tests
{
    public class InMemoryFileSystemTests
    {
        private IAttributeFileSystem GetFileSystem()
        {
            return new InMemoryFileSystem();
        }

        [Fact]
        public void CreateFileWithNoParentDirectory()
        {
            IAttributeFileSystem fs = GetFileSystem();

            Result rc = fs.CreateFile("/dir/file", 0, CreateFileOptions.None);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void RootDirectoryHasCorrectEntryType()
        {
            IAttributeFileSystem fs = GetFileSystem();

            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type);
        }

        [Fact]
        public void CreatedFileHasCorrectSize()
        {
            const long expectedSize = 12345;

            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file", expectedSize, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file", OpenMode.Read);
            Result rc = file.GetSize(out long fileSize);

            Assert.True(rc.IsSuccess());
            Assert.Equal(expectedSize, fileSize);
        }

        [Fact]
        public void ReadDataWrittenToFileAfterReopening()
        {
            var data = new byte[] { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };

            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file", data.Length, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file", OpenMode.Write);
            file.Write(0, data, WriteOption.None);
            file.Dispose();

            var readData = new byte[data.Length];

            fs.OpenFile(out file, "/file", OpenMode.Read);
            Result rc = file.Read(out long bytesRead, 0, readData, ReadOption.None);
            file.Dispose();

            Assert.True(rc.IsSuccess());
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data, readData);
        }

        [Fact]
        public void ReadDataWrittenToFileAfterRenaming()
        {
            var data = new byte[] { 7, 4, 1, 0, 8, 5, 2, 9, 6, 3 };

            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file", data.Length, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file", OpenMode.Write);
            file.Write(0, data, WriteOption.None);
            file.Dispose();

            fs.RenameFile("/file", "/renamed");

            var readData = new byte[data.Length];

            fs.OpenFile(out file, "/renamed", OpenMode.Read);
            Result rc = file.Read(out long bytesRead, 0, readData, ReadOption.None);
            file.Dispose();

            Assert.True(rc.IsSuccess());
            Assert.Equal(data.Length, bytesRead);
            Assert.Equal(data, readData);
        }

        [Fact]
        public void OpenFileAsDirectory()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rc = fs.OpenDirectory(out _, "/file", OpenDirectoryMode.All);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void OpenDirectoryAsFile()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir");

            Result rc = fs.OpenFile(out _, "/dir", OpenMode.All);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteNonexistentFile()
        {
            IAttributeFileSystem fs = GetFileSystem();

            Result rc = fs.DeleteFile("/file");
            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteNonexistentDirectory()
        {
            IAttributeFileSystem fs = GetFileSystem();

            Result rc = fs.DeleteDirectory("/dir");
            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void DeleteFile()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteFile("/file");
            Result rcEntry = fs.GetEntryType(out _, "/file");

            Assert.True(rcDelete.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry);
        }

        [Fact]
        public void DeleteFileWithSiblingA()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/file2", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteFile("/file2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/file1");
            Result rcEntry2 = fs.GetEntryType(out _, "/file2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.File, dir1Type);
        }

        [Fact]
        public void DeleteFileWithSiblingB()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file2", 0, CreateFileOptions.None);
            fs.CreateFile("/file1", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteFile("/file2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/file1");
            Result rcEntry2 = fs.GetEntryType(out _, "/file2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.File, dir1Type);
        }

        [Fact]
        public void DeleteDirectory()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir");

            Result rcDelete = fs.DeleteDirectory("/dir");
            Result rcEntry = fs.GetEntryType(out _, "/dir");

            Assert.True(rcDelete.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry);
        }

        [Fact]
        public void DeleteDirectoryWithSiblingA()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir2");

            Result rcDelete = fs.DeleteDirectory("/dir2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
            Result rcEntry2 = fs.GetEntryType(out _, "/dir2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
        }

        [Fact]
        public void DeleteDirectoryWithSiblingB()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir2");
            fs.CreateDirectory("/dir1");

            Result rcDelete = fs.DeleteDirectory("/dir2");
            Result rcEntry1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
            Result rcEntry2 = fs.GetEntryType(out _, "/dir2");

            Assert.True(rcDelete.IsSuccess());
            Assert.True(rcEntry1.IsSuccess());
            Assert.Equal(ResultFs.PathNotFound.Value, rcEntry2);

            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
        }

        [Fact]
        public void DeleteDirectoryWithChildren()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir");
            fs.CreateFile("/dir/file", 0, CreateFileOptions.None);

            Result rc = fs.DeleteDirectory("/dir");

            Assert.Equal(ResultFs.DirectoryNotEmpty.Value, rc);
        }

        [Fact]
        public void DeleteDirectoryRecursively()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir");
            fs.CreateDirectory("/dir/dir2");
            fs.CreateFile("/dir/file1", 0, CreateFileOptions.None);

            Result rcDelete = fs.DeleteDirectoryRecursively("/dir");

            Result rcDir1Type = fs.GetEntryType(out _, "/dir");
            Result rcDir2Type = fs.GetEntryType(out _, "/dir/dir2");
            Result rcFileType = fs.GetEntryType(out _, "/dir/file1");

            Assert.True(rcDelete.IsSuccess());

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcDir2Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcFileType);
        }

        [Fact]
        public void CleanDirectoryRecursively()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir");
            fs.CreateDirectory("/dir/dir2");
            fs.CreateFile("/dir/file1", 0, CreateFileOptions.None);

            Result rcDelete = fs.CleanDirectoryRecursively("/dir");

            Result rcDir1Type = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir");
            Result rcDir2Type = fs.GetEntryType(out _, "/dir/dir2");
            Result rcFileType = fs.GetEntryType(out _, "/dir/file1");

            Assert.True(rcDelete.IsSuccess());

            Assert.True(rcDir1Type.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir1Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir2Type);
            Assert.Equal(ResultFs.PathNotFound.Value, rcFileType);
        }

        [Fact]
        public void CreateFile()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file", 0, CreateFileOptions.None);
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/file");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateFileWithTrailingSlash()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file/", 0, CreateFileOptions.None);
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/file/");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateDirectory()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir");
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/dir");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type);
        }

        [Fact]
        public void CreateDirectoryWithTrailingSlash()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir/");
            Result rc = fs.GetEntryType(out DirectoryEntryType type, "/dir/");

            Assert.True(rc.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type);
        }

        [Fact]
        public void CreateMultipleDirectories()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir2");
            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1");
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2");

            Assert.True(rc1.IsSuccess());
            Assert.True(rc2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type1);
            Assert.Equal(DirectoryEntryType.Directory, type2);
        }

        [Fact]
        public void CreateMultipleNestedDirectories()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir2");

            fs.CreateDirectory("/dir1/dir1a");
            fs.CreateDirectory("/dir2/dir2a");
            Result rc1 = fs.GetEntryType(out DirectoryEntryType type1, "/dir1/dir1a");
            Result rc2 = fs.GetEntryType(out DirectoryEntryType type2, "/dir2/dir2a");

            Assert.True(rc1.IsSuccess());
            Assert.True(rc2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, type1);
            Assert.Equal(DirectoryEntryType.Directory, type2);
        }

        [Fact]
        public void CreateDirectoryWithAttribute()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir1", NxFileAttributes.None);
            fs.CreateDirectory("/dir2", NxFileAttributes.Archive);

            Result rc1 = fs.GetFileAttributes("/dir1", out NxFileAttributes type1);
            Result rc2 = fs.GetFileAttributes("/dir2", out NxFileAttributes type2);

            Assert.True(rc1.IsSuccess());
            Assert.True(rc2.IsSuccess());

            Assert.Equal(NxFileAttributes.Directory, type1);
            Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, type2);
        }

        [Fact]
        public void RenameFile()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file1", 12345, CreateFileOptions.None);

            Result rcRename = fs.RenameFile("/file1", "/file2");

            Result rcOpen = fs.OpenFile(out IFile file, "/file2", OpenMode.All);
            Result rcSize = file.GetSize(out long fileSize);

            Result rcOldType = fs.GetEntryType(out _, "/file1");

            Assert.True(rcRename.IsSuccess());
            Assert.True(rcOpen.IsSuccess());
            Assert.True(rcSize.IsSuccess());

            Assert.Equal(12345, fileSize);
            Assert.Equal(ResultFs.PathNotFound.Value, rcOldType);
        }

        [Fact]
        public void RenameFileWhenDestExists()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateFile("/file1", 12345, CreateFileOptions.None);
            fs.CreateFile("/file2", 54321, CreateFileOptions.None);

            Result rcRename = fs.RenameFile("/file1", "/file2");

            Result rcFile1 = fs.GetEntryType(out DirectoryEntryType file1Type, "/file1");
            Result rcFile2 = fs.GetEntryType(out DirectoryEntryType file2Type, "/file2");

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rcRename);

            Assert.True(rcFile1.IsSuccess());
            Assert.True(rcFile2.IsSuccess());
            Assert.Equal(DirectoryEntryType.File, file1Type);
            Assert.Equal(DirectoryEntryType.File, file2Type);
        }

        [Fact]
        public void RenameDirectory()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir1");
            Result rcRename = fs.RenameDirectory("/dir1", "/dir2");

            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");
            Result rcDir1 = fs.GetEntryType(out _, "/dir1");

            Assert.True(rcRename.IsSuccess());

            Assert.True(rcDir2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1);
        }

        [Fact]
        public void RenameDirectoryWithChildren()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir1/dirC");
            fs.CreateFile("/dir1/file1", 0, CreateFileOptions.None);

            Result rcRename = fs.RenameDirectory("/dir1", "/dir2");

            // Check that renamed structure exists
            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");
            Result rcDirC = fs.GetEntryType(out DirectoryEntryType dir1CType, "/dir2/dirC");
            Result rcFile1 = fs.GetEntryType(out DirectoryEntryType file1Type, "/dir2/file1");

            // Check that old structure doesn't exist
            Result rcDir1 = fs.GetEntryType(out _, "/dir1");
            Result rcDirCOld = fs.GetEntryType(out _, "/dir1/dirC");
            Result rcFile1Old = fs.GetEntryType(out _, "/dir1/file1");

            Assert.True(rcRename.IsSuccess());

            Assert.True(rcDir2.IsSuccess());
            Assert.True(rcDirC.IsSuccess());
            Assert.True(rcFile1.IsSuccess());

            Assert.Equal(DirectoryEntryType.Directory, dir2Type);
            Assert.Equal(DirectoryEntryType.Directory, dir1CType);
            Assert.Equal(DirectoryEntryType.File, file1Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1);
            Assert.Equal(ResultFs.PathNotFound.Value, rcDirCOld);
            Assert.Equal(ResultFs.PathNotFound.Value, rcFile1Old);
        }

        [Fact]
        public void RenameDirectoryToDifferentParent()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/parent1");
            fs.CreateDirectory("/parent2");
            fs.CreateDirectory("/parent1/dir1");

            Result rcRename = fs.RenameDirectory("/parent1/dir1", "/parent2/dir2");

            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/parent2/dir2");
            Result rcDir1 = fs.GetEntryType(out _, "/parent1/dir1");

            Assert.True(rcRename.IsSuccess());

            Assert.Equal(Result.Success, rcDir2);
            Assert.True(rcDir2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);

            Assert.Equal(ResultFs.PathNotFound.Value, rcDir1);
        }

        [Fact]
        public void RenameDirectoryWhenDestExists()
        {
            IAttributeFileSystem fs = GetFileSystem();

            fs.CreateDirectory("/dir1");
            fs.CreateDirectory("/dir2");

            Result rcRename = fs.RenameDirectory("/dir1", "/dir2");

            Result rcDir1 = fs.GetEntryType(out DirectoryEntryType dir1Type, "/dir1");
            Result rcDir2 = fs.GetEntryType(out DirectoryEntryType dir2Type, "/dir2");

            Assert.Equal(ResultFs.PathAlreadyExists.Value, rcRename);

            Assert.True(rcDir1.IsSuccess());
            Assert.True(rcDir2.IsSuccess());
            Assert.Equal(DirectoryEntryType.Directory, dir1Type);
            Assert.Equal(DirectoryEntryType.Directory, dir2Type);
        }

        [Fact]
        public void SetFileSize()
        {
            IAttributeFileSystem fs = GetFileSystem();
            fs.CreateFile("/file", 0, CreateFileOptions.None);

            fs.OpenFile(out IFile file, "/file", OpenMode.All);
            Result rc = file.SetSize(54321);
            file.Dispose();

            fs.OpenFile(out file, "/file", OpenMode.All);
            file.GetSize(out long fileSize);
            file.Dispose();

            Assert.True(rc.IsSuccess());
            Assert.Equal(54321, fileSize);
        }

        [Fact]
        public void SetFileAttributes()
        {
            IAttributeFileSystem fs = GetFileSystem();
            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rcSet = fs.SetFileAttributes("/file", NxFileAttributes.Archive);
            Result rcGet = fs.GetFileAttributes("/file", out NxFileAttributes attributes);

            Assert.True(rcSet.IsSuccess());
            Assert.True(rcGet.IsSuccess());
            Assert.Equal(NxFileAttributes.Archive, attributes);
        }

        [Fact]
        public void IterateDirectory()
        {
            IAttributeFileSystem fs = GetFileSystem();
            fs.CreateDirectory("/dir");
            fs.CreateDirectory("/dir/dir1");
            fs.CreateFile("/dir/dir1/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/dir/file1", 0, CreateFileOptions.None);
            fs.CreateFile("/dir/file2", 0, CreateFileOptions.None);

            Result rc = fs.OpenDirectory(out IDirectory dir, "/dir", OpenDirectoryMode.All);
            Assert.True(rc.IsSuccess());

            var entry1 = new DirectoryEntry();
            var entry2 = new DirectoryEntry();
            var entry3 = new DirectoryEntry();
            var entry4 = new DirectoryEntry();

            Assert.True(dir.Read(out long entriesRead1, SpanHelpers.AsSpan(ref entry1)).IsSuccess());
            Assert.True(dir.Read(out long entriesRead2, SpanHelpers.AsSpan(ref entry2)).IsSuccess());
            Assert.True(dir.Read(out long entriesRead3, SpanHelpers.AsSpan(ref entry3)).IsSuccess());
            Assert.True(dir.Read(out long entriesRead4, SpanHelpers.AsSpan(ref entry4)).IsSuccess());

            Assert.Equal(1, entriesRead1);
            Assert.Equal(1, entriesRead2);
            Assert.Equal(1, entriesRead3);
            Assert.Equal(0, entriesRead4);

            bool dir1Read = false;
            bool file1Read = false;
            bool file2Read = false;

            // Entries are not guaranteed to be in any particular order
            CheckEntry(ref entry1);
            CheckEntry(ref entry2);
            CheckEntry(ref entry3);

            Assert.True(dir1Read);
            Assert.True(file1Read);
            Assert.True(file2Read);

            void CheckEntry(ref DirectoryEntry entry)
            {
                switch (StringUtils.Utf8ZToString(entry.Name))
                {
                    case "dir1":
                        Assert.False(dir1Read);
                        Assert.Equal(DirectoryEntryType.Directory, entry.Type);

                        dir1Read = true;
                        break;

                    case "file1":
                        Assert.False(file1Read);
                        Assert.Equal(DirectoryEntryType.File, entry.Type);

                        file1Read = true;
                        break;

                    case "file2":
                        Assert.False(file2Read);
                        Assert.Equal(DirectoryEntryType.File, entry.Type);

                        file2Read = true;
                        break;

                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }
    }
}
