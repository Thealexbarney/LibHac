using System;
using System.Linq;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Shim;
using LibHac.FsSrv.Impl;
using LibHac.Ncm;
using LibHac.Time;
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

            Assert.Result(ResultFs.PortSdCardNoDevice, fs.CreateCacheStorage(applicationId, SaveDataSpaceId.SdCache, applicationId.Value, 0, 0, SaveDataFlags.None));
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

            ushort[] expectedIndexes = { 0, 1 };
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

        [Theory]
        [InlineData(AccessControlBits.Bits.SystemSaveData)]
        [InlineData(AccessControlBits.Bits.None)]
        public void CreateSystemSaveData_HasBuiltInSystemPermission_SaveIsCreatedInSystem(AccessControlBits.Bits permissions)
        {
            ulong saveId = 0x8000000001234000;

            Horizon hos = FileSystemServerFactory.CreateHorizonServer();
            
            var mainProgramId = new ProgramId(0x123456);

            HorizonClient client = hos.CreateHorizonClient(new ProgramLocation(mainProgramId, StorageId.BuiltInSystem),
                permissions);

            HorizonClient privilegedClient = hos.CreatePrivilegedHorizonClient();

            // Create the save
            if (permissions.HasFlag(AccessControlBits.Bits.SystemSaveData))
            {
                Assert.Success(client.Fs.CreateSystemSaveData(saveId, 0x1000, 0x1000, SaveDataFlags.None));
            }
            else
            {
                // Creation should fail if we don't have the right permissions.
                Assert.Failure(client.Fs.CreateSystemSaveData(saveId, 0x1000, 0x1000, SaveDataFlags.None));
                return;
            }

            // Make sure it was placed in the System save space with the right info.
            Assert.Success(privilegedClient.Fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.System));

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
        public void CreateSaveData_DoesNotExist_HasCorrectOwnerId()
        {
            uint ownerId = 1;

            var applicationId = new Ncm.ApplicationId(ownerId);
            var userId = new UserId(5, 4);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create the save
            Assert.Success(fs.CreateSaveData(applicationId, userId, ownerId, 0x1000, 0x1000, SaveDataFlags.None));

            // Get the created save data's ID
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);

            // Get the created save data's owner ID
            Assert.Success(fs.GetSaveDataOwnerId(out ulong actualOwnerId, info[0].SaveDataId));

            Assert.Equal(ownerId, actualOwnerId);
        }

        [Fact]
        public void CreateSaveData_DoesNotExist_HasCorrectFlags()
        {
            SaveDataFlags flags = SaveDataFlags.KeepAfterRefurbishment | SaveDataFlags.NeedsSecureDelete;

            var applicationId = new Ncm.ApplicationId(1);
            var userId = new UserId(5, 4);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create the save
            Assert.Success(fs.CreateSaveData(applicationId, userId, 0, 0x1000, 0x1000, flags));

            // Get the created save data's ID
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);

            // Get the created save data's flags
            Assert.Success(fs.GetSaveDataFlags(out SaveDataFlags actualFlags, info[0].SaveDataId));

            Assert.Equal(flags, actualFlags);
        }

        [Fact]
        public void CreateSaveData_DoesNotExist_HasCorrectSizes()
        {
            long availableSize = 0x220000;
            long journalSize = 0x120000;

            var applicationId = new Ncm.ApplicationId(1);
            var userId = new UserId(5, 4);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create the save
            Assert.Success(fs.CreateSaveData(applicationId, userId, 0, availableSize, journalSize, SaveDataFlags.None));

            // Get the created save data's ID
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);

            // Get the created save data's sizes
            Assert.Success(fs.GetSaveDataAvailableSize(out long actualAvailableSize, info[0].SaveDataId));
            Assert.Success(fs.GetSaveDataJournalSize(out long actualJournalSize, info[0].SaveDataId));

            Assert.Equal(availableSize, actualAvailableSize);
            Assert.Equal(journalSize, actualJournalSize);
        }

        [Fact]
        public void CreateSaveData_FromSubProgram_CreatesSaveDataForMainProgram()
        {
            Horizon hos = FileSystemServerFactory.CreateHorizonServer();

            Span<ProgramIndexMapInfo> mapInfo = stackalloc ProgramIndexMapInfo[5];

            var mainProgramId = new ProgramId(0x123456);
            var programId = new ProgramId(mainProgramId.Value + 2);

            for (int i = 0; i < mapInfo.Length; i++)
            {
                mapInfo[i].MainProgramId = mainProgramId;
                mapInfo[i].ProgramId = new ProgramId(mainProgramId.Value + (uint)i);
                mapInfo[i].ProgramIndex = (byte)i;
            }

            HorizonClient client = hos.CreatePrivilegedHorizonClient();
            HorizonClient subProgramClient =
                hos.CreateHorizonClient(new ProgramLocation(programId, StorageId.BuiltInUser),
                    AccessControlBits.Bits.CreateSaveData);

            Assert.Success(client.Fs.RegisterProgramIndexMapInfo(mapInfo));

            Assert.Success(subProgramClient.Fs.CreateSaveData(Ncm.ApplicationId.InvalidId, UserId.InvalidId, 0, 0x4000,
                0x4000, SaveDataFlags.None));

            // Get the created save data's ID
            Assert.Success(client.Fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);

            Assert.Equal(mainProgramId, info[0].ProgramId);
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

            Assert.Success(iterator.ReadSaveDataInfo(out long readCountFinal, SpanHelpers.AsSpan(ref info)));

            Assert.Equal(0, readCountFinal);
        }

        [Fact]
        public void ReadSaveDataInfo_WhenFilteringSavesByUserId_IteratorReturnsAllMatchingSaves()
        {
            const int count = 10;
            const int countUser1 = 5;

            var user1Id = new UserId(0x1234, 0x5678);
            var user2Id = new UserId(0x1122, 0x3344);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            for (int i = 1; i <= countUser1; i++)
            {
                var applicationId = new Ncm.ApplicationId((uint)i);
                Assert.Success(fs.CreateSaveData(applicationId, user1Id, 0, 0x4000, 0x4000, SaveDataFlags.None));
            }

            for (int i = countUser1 + 1; i <= count; i++)
            {
                var applicationId = new Ncm.ApplicationId((uint)i);
                Assert.Success(fs.CreateSaveData(applicationId, user2Id, 0, 0x4000, 0x4000, SaveDataFlags.None));
            }

            Assert.Success(SaveDataFilter.Make(out SaveDataFilter filter, default, default, user2Id, default, default));

            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User, in filter));

            var info = new SaveDataInfo();
            for (int i = countUser1 + 1; i <= count; i++)
            {
                Assert.Success(iterator.ReadSaveDataInfo(out long readCount, SpanHelpers.AsSpan(ref info)));

                Assert.Equal(1, readCount);
                Assert.Equal((ulong)i, info.ProgramId.Value);
                Assert.Equal(user2Id, info.UserId);
            }

            Assert.Success(iterator.ReadSaveDataInfo(out long readCountFinal, SpanHelpers.AsSpan(ref info)));

            Assert.Equal(0, readCountFinal);
        }

        [Fact]
        public void GetSaveDataCommitId_AfterSetSaveDataCommitIdIsCalled_ReturnsSetCommitId()
        {
            long commitId = 46506854;

            var applicationId = new Ncm.ApplicationId(1);
            var userId = new UserId(5, 4);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create the save
            Assert.Success(fs.CreateSaveData(applicationId, userId, 0, 0x1000, 0x1000, SaveDataFlags.None));

            // Get the created save data's ID
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);

            // Set the new commit ID
            Assert.Success(fs.SetSaveDataCommitId(info[0].SpaceId, info[0].SaveDataId, commitId));

            Assert.Success(fs.GetSaveDataCommitId(out long actualCommitId, info[0].SpaceId, info[0].SaveDataId));

            Assert.Equal(commitId, actualCommitId);
        }

        [Fact]
        public void GetSaveDataTimeStamp_AfterSetSaveDataTimeStampIsCalled_ReturnsSetTimeStamp()
        {
            var timeStamp = new PosixTime(12345678);

            var applicationId = new Ncm.ApplicationId(1);
            var userId = new UserId(5, 4);

            FileSystemClient fs = FileSystemServerFactory.CreateClient(true);

            // Create the save
            Assert.Success(fs.CreateSaveData(applicationId, userId, 0, 0x1000, 0x1000, SaveDataFlags.None));

            // Get the created save data's ID
            Assert.Success(fs.OpenSaveDataIterator(out SaveDataIterator iterator, SaveDataSpaceId.User));

            var info = new SaveDataInfo[2];
            iterator.ReadSaveDataInfo(out long entriesRead, info);

            Assert.Equal(1, entriesRead);

            // Set the new timestamp
            Assert.Success(fs.SetSaveDataTimeStamp(info[0].SpaceId, info[0].SaveDataId, timeStamp));

            Assert.Success(fs.GetSaveDataTimeStamp(out PosixTime actualTimeStamp, info[0].SpaceId, info[0].SaveDataId));

            Assert.Equal(timeStamp, actualTimeStamp);
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
