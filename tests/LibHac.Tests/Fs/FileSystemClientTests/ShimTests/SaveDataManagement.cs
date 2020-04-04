using System.Linq;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.Ncm;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests
{
    public class SaveDataManagement
    {
        [Fact]
        public void CreateCacheStorage_InUserSaveSpace_StorageIsCreated()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId, 0, 0, SaveDataFlags.None));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].TitleId);
        }

        [Fact]
        public void CreateCacheStorage_InSdCacheSaveSpace_StorageIsCreated()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId, 0, 0, SaveDataFlags.None));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.SdCache);

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].TitleId);
        }

        [Fact]
        public void CreateCacheStorage_InSdCacheSaveSpaceWhenNoSdCard_ReturnsSdCardNotFound()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

            Assert.Result(ResultFs.SdCardNotFound, fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId, 0, 0, SaveDataFlags.None));
        }

        [Fact]
        public void CreateCacheStorage_AlreadyExists_ReturnsPathAlreadyExists()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId, 0, 0, SaveDataFlags.None));
            Assert.Result(ResultFs.PathAlreadyExists, fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId, 0, 0, SaveDataFlags.None));
        }

        [Fact]
        public void CreateCacheStorage_WithIndex_CreatesMultiple()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId, 0, 0, 0, SaveDataFlags.None));
            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId, 1, 0, 0, SaveDataFlags.None));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[3];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(2, entriesRead);
            Assert.Equal(applicationId, info[0].TitleId);
            Assert.Equal(applicationId, info[1].TitleId);

            var expectedIndexes = new short[] { 0, 1 };
            short[] actualIndexes = info.Take(2).Select(x => x.Index).OrderBy(x => x).ToArray();

            Assert.Equal(expectedIndexes, actualIndexes);
        }

        [Fact]
        public void CreateBcatSaveData_DoesNotExist_SaveIsCreated()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateBcatSaveData(applicationId, 0x400000));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].TitleId);
            Assert.Equal(SaveDataType.Bcat, info[0].Type);
        }

        [Fact]
        public void CreateBcatSaveData_AlreadyExists_ReturnsPathAlreadyExists()
        {
            var applicationId = new TitleId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateBcatSaveData(applicationId, 0x400000));
            Assert.Result(ResultFs.PathAlreadyExists, fs.CreateBcatSaveData(applicationId, 0x400000));
        }
    }
}
