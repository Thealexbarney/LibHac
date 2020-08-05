using System.Linq;
using LibHac.Fs;
using LibHac.Fs.Shim;
using Xunit;

namespace LibHac.Tests.Fs.FileSystemClientTests.ShimTests
{
    public class SaveDataManagement
    {
        [Fact]
        public void CreateCacheStorage_InUserSaveSpace_StorageIsCreated()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, SaveDataFlags.None));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
        }

        [Fact]
        public void CreateCacheStorage_InSdCacheSaveSpace_StorageIsCreated()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId.Value, 0, 0, SaveDataFlags.None));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.SdCache);

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
        }

        [Fact]
        public void CreateCacheStorage_InSdCacheSaveSpaceWhenNoSdCard_ReturnsSdCardNotFound()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(false);

            Assert.Result(ResultFs.SdCardNotFound, fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId.Value, 0, 0, SaveDataFlags.None));
        }

        [Fact]
        public void CreateCacheStorage_AlreadyExists_ReturnsPathAlreadyExists()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, SaveDataFlags.None));
            Assert.Result(ResultFs.PathAlreadyExists, fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, SaveDataFlags.None));
        }

        [Fact]
        public void CreateCacheStorage_WithIndex_CreatesMultiple()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 0, 0, 0, SaveDataFlags.None));
            Assert.Success(fs.CreateCacheStorage(applicationId, SaveDataSpaceId.User, applicationId.Value, 1, 0, 0, SaveDataFlags.None));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[3];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(2, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(applicationId, info[1].ProgramId);

            var expectedIndexes = new short[] { 0, 1 };
            short[] actualIndexes = info.Take(2).Select(x => x.Index).OrderBy(x => x).ToArray();

            Assert.Equal(expectedIndexes, actualIndexes);
        }

        [Fact]
        public void CreateBcatSaveData_DoesNotExist_SaveIsCreated()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateBcatSaveData(applicationId, 0x400000));

            fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User);

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(SaveDataType.Bcat, info[0].Type);
        }

        [Fact]
        public void CreateBcatSaveData_AlreadyExists_ReturnsPathAlreadyExists()
        {
            var applicationId = new Ncm.ApplicationId(1);
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateBcatSaveData(applicationId, 0x400000));
            Assert.Result(ResultFs.PathAlreadyExists, fs.CreateBcatSaveData(applicationId, 0x400000));
        }

        [Fact]
        public void CreateSystemSaveData_DoesNotExist_SaveIsCreatedInSystem()
        {
            ulong saveId = 0x8000000001234000;

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create the save
            Assert.Success(fs.CreateSystemSaveData(saveId, 0x1000, 0x1000, SaveDataFlags.None));

            // Make sure it was placed in the System save space with the right info.
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.System));

            var info = new SaveDataInfo[2];
            Assert.Success(iterator.ReadSaveDataInfo(out long entriesRead, info));

            Assert.Equal(1, entriesRead);
            Assert.Equal(SaveDataType.System, info[0].Type);
            Assert.Equal(SaveDataSpaceId.System, info[0].SpaceId);
            Assert.Equal(saveId, info[0].StaticSaveDataId);
            Assert.Equal(saveId, info[0].SaveDataId);
            Assert.Equal(SaveDataState.Normal, info[0].State);
        }

        [Fact]
        public void CreateSaveData_DoesNotExist_SaveIsCreated()
        {
            var applicationId = new Ncm.ApplicationId(1);
            var userId = new UserId(5, 4);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(fs.CreateSaveData(applicationId, userId, 0, 0x1000, 0x1000, SaveDataFlags.None));

            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);
            Assert.Equal(applicationId, info[0].ProgramId);
            Assert.Equal(SaveDataType.Account, info[0].Type);
            Assert.Equal(userId, info[0].UserId);
        }

        [Fact]
        public void DeleteSaveData_DoesNotExist_ReturnsTargetNotFound()
        {
            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Result(ResultFs.TargetNotFound, fs.DeleteSaveData(1));
        }

        [Fact]
        public void DeleteSaveData_SaveExistsInUserSaveSpace_SaveIsDeleted()
        {
            var applicationId = new Ncm.ApplicationId(1);
            var userId = new UserId(5, 4);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create the save
            Assert.Success(fs.CreateSaveData(applicationId, userId, 0, 0x1000, 0x1000, SaveDataFlags.None));

            // Get the ID of the save
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[1];
            Assert.Success(iterator.ReadSaveDataInfo(out _, info));

            // Delete the save
            Assert.Success(fs.DeleteSaveData(info[0].SaveDataId));

            // Iterate saves again
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator2, SaveDataSpaceId.User));
            Assert.Success(iterator2.ReadSaveDataInfo(out long entriesRead, info));

            // Make sure no saves were returned
            Assert.Equal(0, entriesRead);
        }
    }
}
