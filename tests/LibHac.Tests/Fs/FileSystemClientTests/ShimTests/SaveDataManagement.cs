using System;
using System.Linq;
using LibHac.Common;
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

            var expectedIndexes = new ushort[] { 0, 1 };
            ushort[] actualIndexes = info.Take(2).Select(x => x.Index).OrderBy(x => x).ToArray();

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

        [Theory]
        [InlineData(2, 3, 2)]
        [InlineData(3, 3, 4)]
        [InlineData(5, 3, 5)]
        public void DeleteSaveData_SaveDataIteratorsAreOpen_IteratorsPointToSameEntry(int nextEntryWhenRemoving,
            int entryToRemove, int expectedNextEntry)
        {
            const int count = 20;

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create saves
            Assert.Success(PopulateSaveData(fs, count));

            // Open an iterator
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            // Skip ahead a few entries. Entries start counting at 1, so subtract 1
            var infos = new SaveDataInfo[nextEntryWhenRemoving - 1];
            Assert.Success(iterator.ReadSaveDataInfo(out long readCount, infos));
            Assert.Equal(infos.Length, readCount);

            // Delete the save
            Assert.Success(fs.DeleteSaveData(SaveDataSpaceId.User, (ulong)entryToRemove));

            // Check the program ID of the next entry
            Assert.Success(iterator.ReadSaveDataInfo(out long readCount2, infos.AsSpan(0, 1)));
            Assert.Equal(1, readCount2);

            Assert.Equal((ulong)expectedNextEntry, infos[0].ProgramId.Value);
        }

        [Theory]
        [InlineData(6, 7, 6)]
        [InlineData(8, 7, 8)]
        public void CreateSaveData_SaveDataIteratorsAreOpen_IteratorsPointToSameEntry(int nextEntryWhenAdding,
            int entryToAdd, int expectedNextEntry)
        {
            // Static save IDs must have the high bit set
            const ulong mask = 0x8000000000000000;
            const int count = 10;

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create saves
            for (int i = 0; i < count * 2; i++)
            {
                Assert.Success(fs.CreateSystemSaveData(SaveDataSpaceId.User, ((ulong)i * 2) | mask, 0, 0x4000,
                    0x4000, SaveDataFlags.None));
            }

            // Open an iterator
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            // Skip ahead a few entries. We skipped 0 and added every other ID, so divide by 2 and subtract 1
            var infos = new SaveDataInfo[nextEntryWhenAdding / 2 - 1];
            Assert.Success(iterator.ReadSaveDataInfo(out long readCount, infos));
            Assert.Equal(infos.Length, readCount);

            // Create the save
            Assert.Success(fs.CreateSystemSaveData(SaveDataSpaceId.User, (uint)entryToAdd | mask, (ulong)entryToAdd, 0x4000,
                0x4000, SaveDataFlags.None));

            // Check the save ID of the next entry
            Assert.Success(iterator.ReadSaveDataInfo(out long readCount2, infos.AsSpan(0, 1)));
            Assert.Equal(1, readCount2);

            Assert.Equal((uint)expectedNextEntry | mask, infos[0].SaveDataId);
        }

        [Fact]
        public void OpenSaveDataIterator_MultipleSavesExist_IteratorReturnsSavesInOrder()
        {
            const int count = 20;
            const int rngSeed = 359;

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            Assert.Success(PopulateSaveData(fs, count, rngSeed));

            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo();
            for (int i = 0; i < count; i++)
            {
                Assert.Success(iterator.ReadSaveDataInfo(out long readCount, SpanHelpers.AsSpan(ref info)));

                Assert.Equal(1, readCount);
                Assert.Equal((ulong)i, info.ProgramId.Value);
            }
        }

        private static Result PopulateSaveData(FileSystemClient fs, int count, int seed = -1)
        {
            if (seed == -1)
            {
                for (int i = 1; i <= count; i++)
                {
                    var applicationId = new Ncm.ApplicationId((uint)i);
                    Result rc = fs.CreateSaveData(applicationId, UserId.InvalidId, 0, 0x4000, 0x4000, SaveDataFlags.None);
                    if (rc.IsFailure()) return rc;
                }
            }
            else
            {
                var rng = new FullCycleRandom(count, seed);

                for (int i = 1; i <= count; i++)
                {
                    var applicationId = new Ncm.ApplicationId((uint)rng.Next());
                    Result rc = fs.CreateSaveData(applicationId, UserId.InvalidId, 0, 0x4000, 0x4000, SaveDataFlags.None);
                    if (rc.IsFailure()) return rc;
                }
            }

            return Result.Success;
        }
    }
}
