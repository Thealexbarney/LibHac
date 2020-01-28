using LibHac.Fs;
using Xunit;

namespace LibHac.Tests.Fs.IFileSystemTestBase
{
    public abstract class IAttributeFileSystemTests : IFileSystemTests
    {
        protected abstract IAttributeFileSystem CreateAttributeFileSystem();

        [Fact]
        public void CreateDirectory_WithoutArchiveAttribute_ArchiveFlagIsNotSet()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();

            Assert.True(fs.CreateDirectory("/dir", NxFileAttributes.None).IsSuccess());

            Assert.True(fs.GetFileAttributes(out NxFileAttributes attributes, "/dir").IsSuccess());
            Assert.Equal(NxFileAttributes.Directory, attributes);
        }

        [Fact]
        public void CreateDirectory_WithArchiveAttribute_ArchiveFlagIsSet()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();

            Assert.True(fs.CreateDirectory("/dir", NxFileAttributes.Archive).IsSuccess());

            Assert.True(fs.GetFileAttributes(out NxFileAttributes attributes, "/dir").IsSuccess());
            Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, attributes);
        }

        [Fact]
        public void GetFileAttributes_AttributesOnNewFileAreEmpty()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();
            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rc = fs.GetFileAttributes(out NxFileAttributes attributes, "/file");

            Assert.True(rc.IsSuccess());
            Assert.Equal(NxFileAttributes.None, attributes);
        }

        [Fact]
        public void GetFileAttributes_AttributesOnNewDirHaveOnlyDirFlagSet()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();
            fs.CreateDirectory("/dir");

            Result rc = fs.GetFileAttributes(out NxFileAttributes attributes, "/dir");

            Assert.True(rc.IsSuccess());
            Assert.Equal(NxFileAttributes.Directory, attributes);
        }

        [Fact]
        public void GetFileAttributes_PathDoesNotExist_ReturnsPathNotFound()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();

            Result rc = fs.GetFileAttributes(out _, "/path");

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void SetFileAttributes_PathDoesNotExist_ReturnsPathNotFound()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();

            Result rc = fs.SetFileAttributes("/path", NxFileAttributes.None);

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void SetFileAttributes_SetAttributeOnFile()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();
            fs.CreateFile("/file", 0, CreateFileOptions.None);

            Result rcSet = fs.SetFileAttributes("/file", NxFileAttributes.Archive);
            Result rcGet = fs.GetFileAttributes(out NxFileAttributes attributes, "/file");

            Assert.True(rcSet.IsSuccess());
            Assert.True(rcGet.IsSuccess());
            Assert.Equal(NxFileAttributes.Archive, attributes);
        }

        [Fact]
        public void SetFileAttributes_SetAttributeOnDirectory()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();
            fs.CreateDirectory("/dir");

            Result rcSet = fs.SetFileAttributes("/dir", NxFileAttributes.Archive);
            Result rcGet = fs.GetFileAttributes(out NxFileAttributes attributes, "/dir");

            Assert.True(rcSet.IsSuccess());
            Assert.True(rcGet.IsSuccess());
            Assert.Equal(NxFileAttributes.Directory | NxFileAttributes.Archive, attributes);
        }

        [Fact]
        public void GetFileSize_ReadNewFileSize()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();

            fs.CreateFile("/file", 845, CreateFileOptions.None);

            Assert.True(fs.GetFileSize(out long fileSize, "/file").IsSuccess());
            Assert.Equal(845, fileSize);
        }

        [Fact]
        public void GetFileSize_PathDoesNotExist_ReturnsPathNotFound()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();

            Result rc = fs.GetFileSize(out _, "/path");

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }

        [Fact]
        public void GetFileSize_PathIsDirectory_ReturnsPathNotFound()
        {
            IAttributeFileSystem fs = CreateAttributeFileSystem();
            fs.CreateDirectory("/dir");

            Result rc = fs.GetFileSize(out _, "/dir");

            Assert.Equal(ResultFs.PathNotFound.Value, rc);
        }
    }
}
