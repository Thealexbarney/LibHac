﻿using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LibHac.Common;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSrv;
using LibHac.FsSystem;
using LibHac.Tests.Fs.IFileSystemTestBase;
using Xunit;

namespace LibHac.Tests.Fs
{
    public class DirectorySaveDataFileSystemTests : CommittableIFileSystemTests
    {
        protected override IFileSystem CreateFileSystem()
        {
            return CreateFileSystemInternal().saveFs;
        }

        protected override IReopenableFileSystemCreator GetFileSystemCreator()
        {
            return new DirectorySaveDataFileSystemCreator();
        }

        private class DirectorySaveDataFileSystemCreator : IReopenableFileSystemCreator
        {
            private IFileSystem BaseFileSystem { get; }

            public DirectorySaveDataFileSystemCreator()
            {
                BaseFileSystem = new InMemoryFileSystem();
            }

            public IFileSystem Create()
            {
                CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, BaseFileSystem, true, true, true)
                    .ThrowIfFailure();

                return saveFs;
            }
        }

        public static Result CreateDirSaveFs(out DirectorySaveDataFileSystem created, IFileSystem baseFileSystem,
            ISaveDataCommitTimeStampGetter timeStampGetter, RandomDataGenerator randomGenerator,
            bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled,
            FileSystemClient fsClient)
        {
            var obj = new DirectorySaveDataFileSystem(baseFileSystem, fsClient);
            Result rc = obj.Initialize(timeStampGetter, randomGenerator, isJournalingSupported, isMultiCommitSupported,
                isJournalingEnabled);

            if (rc.IsSuccess())
            {
                created = obj;
                return Result.Success;
            }

            obj.Dispose();
            UnsafeHelpers.SkipParamInit(out created);
            return rc;
        }

        public static Result CreateDirSaveFs(out DirectorySaveDataFileSystem created, IFileSystem baseFileSystem,
            bool isJournalingSupported, bool isMultiCommitSupported, bool isJournalingEnabled)
        {
            return CreateDirSaveFs(out created, baseFileSystem, null, null, isJournalingSupported, isMultiCommitSupported,
                isJournalingEnabled, null);
        }

        private (IFileSystem baseFs, DirectorySaveDataFileSystem saveFs) CreateFileSystemInternal()
        {
            var baseFs = new InMemoryFileSystem();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            return (baseFs, saveFs);
        }

        [Fact]
        public void CreateFile_CreatedInWorkingDirectory()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file", 0, CreateFileOptions.None);

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/1/file"));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void CreateFile_NotCreatedInCommittedDirectory()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file", 0, CreateFileOptions.None);

            Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/0/file"));
        }

        [Fact]
        public void Commit_FileExistsInCommittedDirectory()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file", 0, CreateFileOptions.None);

            Assert.Success(saveFs.Commit());

            Assert.Success(baseFs.GetEntryType(out DirectoryEntryType type, "/0/file"));
            Assert.Equal(DirectoryEntryType.File, type);
        }

        [Fact]
        public void Rollback_FileDoesNotExistInBaseAfterRollback()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file", 0, CreateFileOptions.None);

            // Rollback should succeed
            Assert.Success(saveFs.Rollback());

            // Make sure all the files are gone
            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file"));
            Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/0/file"));
            Assert.Result(ResultFs.PathNotFound, baseFs.GetEntryType(out _, "/1/file"));
        }

        [Fact]
        public void Rollback_DeletedFileIsRestoredInBaseAfterRollback()
        {
            (IFileSystem baseFs, IFileSystem saveFs) = CreateFileSystemInternal();

            saveFs.CreateFile("/file", 0, CreateFileOptions.None);
            saveFs.Commit();
            saveFs.DeleteFile("/file");

            // Rollback should succeed
            Assert.Success(saveFs.Rollback());

            // Make sure all the files are restored
            Assert.Success(saveFs.GetEntryType(out _, "/file"));
            Assert.Success(baseFs.GetEntryType(out _, "/0/file"));
            Assert.Success(baseFs.GetEntryType(out _, "/1/file"));
        }

        [Fact]
        public void Initialize_NormalState_UsesCommittedData()
        {
            var baseFs = new InMemoryFileSystem();

            baseFs.CreateDirectory("/0").ThrowIfFailure();
            baseFs.CreateDirectory("/1").ThrowIfFailure();

            // Set the existing files before initializing the save FS
            baseFs.CreateFile("/0/file1", 0, CreateFileOptions.None).ThrowIfFailure();
            baseFs.CreateFile("/1/file2", 0, CreateFileOptions.None).ThrowIfFailure();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            Assert.Success(saveFs.GetEntryType(out _, "/file1"));
            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file2"));
        }

        [Fact]
        public void Initialize_InterruptedAfterCommitPart1_UsesWorkingData()
        {
            var baseFs = new InMemoryFileSystem();

            baseFs.CreateDirectory("/_").ThrowIfFailure();
            baseFs.CreateDirectory("/1").ThrowIfFailure();

            // Set the existing files before initializing the save FS
            baseFs.CreateFile("/_/file1", 0, CreateFileOptions.None).ThrowIfFailure();
            baseFs.CreateFile("/1/file2", 0, CreateFileOptions.None).ThrowIfFailure();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file1"));
            Assert.Success(saveFs.GetEntryType(out _, "/file2"));
        }

        [Fact]
        public void Initialize_InterruptedDuringCommitPart2_UsesWorkingData()
        {
            var baseFs = new InMemoryFileSystem();

            baseFs.CreateDirectory("/1").ThrowIfFailure();

            // Set the existing files before initializing the save FS
            baseFs.CreateFile("/1/file2", 0, CreateFileOptions.None).ThrowIfFailure();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            Assert.Result(ResultFs.PathNotFound, saveFs.GetEntryType(out _, "/file1"));
            Assert.Success(saveFs.GetEntryType(out _, "/file2"));
        }

        [Fact]
        public void Initialize_InitialExtraDataIsEmpty()
        {
            (IFileSystem _, DirectorySaveDataFileSystem saveFs) = CreateFileSystemInternal();

            Assert.Success(saveFs.ReadExtraData(out SaveDataExtraData extraData));
            Assert.True(SpanHelpers.AsByteSpan(ref extraData).IsZeros());
        }

        [Fact]
        public void WriteExtraData_CanReadBackExtraData()
        {
            (IFileSystem _, DirectorySaveDataFileSystem saveFs) = CreateFileSystemInternal();

            var originalExtraData = new SaveDataExtraData();
            originalExtraData.DataSize = 0x12345;

            Assert.Success(saveFs.WriteExtraData(in originalExtraData));
            Assert.Success(saveFs.ReadExtraData(out SaveDataExtraData extraData));
            Assert.Equal(originalExtraData, extraData);
        }

        [Fact]
        public void Commit_AfterSuccessfulCommit_CanReadCommittedExtraData()
        {
            var baseFs = new InMemoryFileSystem();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            var originalExtraData = new SaveDataExtraData();
            originalExtraData.DataSize = 0x12345;

            saveFs.WriteExtraData(in originalExtraData).ThrowIfFailure();
            Assert.Success(saveFs.CommitExtraData(false));

            saveFs.Dispose();
            CreateDirSaveFs(out saveFs, baseFs, true, true, true).ThrowIfFailure();

            Assert.Success(saveFs.ReadExtraData(out SaveDataExtraData extraData));
            Assert.Equal(originalExtraData, extraData);
        }

        [Fact]
        public void Rollback_WriteExtraDataThenRollback_ExtraDataIsRolledBack()
        {
            var baseFs = new InMemoryFileSystem();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            var originalExtraData = new SaveDataExtraData();
            originalExtraData.DataSize = 0x12345;

            saveFs.WriteExtraData(in originalExtraData).ThrowIfFailure();
            saveFs.CommitExtraData(false).ThrowIfFailure();

            saveFs.Dispose();
            CreateDirSaveFs(out saveFs, baseFs, true, true, true).ThrowIfFailure();

            var newExtraData = new SaveDataExtraData();
            newExtraData.DataSize = 0x67890;

            saveFs.WriteExtraData(in newExtraData).ThrowIfFailure();

            Assert.Success(saveFs.Rollback());
            Assert.Success(saveFs.ReadExtraData(out SaveDataExtraData extraData));

            Assert.Equal(originalExtraData, extraData);
        }

        [Fact]
        public void Rollback_WriteExtraDataThenCloseFs_ExtraDataIsRolledBack()
        {
            var baseFs = new InMemoryFileSystem();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            // Write extra data and close with committing
            var originalExtraData = new SaveDataExtraData();
            originalExtraData.DataSize = 0x12345;

            saveFs.WriteExtraData(in originalExtraData).ThrowIfFailure();
            saveFs.CommitExtraData(false).ThrowIfFailure();

            saveFs.Dispose();
            CreateDirSaveFs(out saveFs, baseFs, true, true, true).ThrowIfFailure();

            // Write a new extra data and close without committing
            var newExtraData = new SaveDataExtraData();
            newExtraData.DataSize = 0x67890;

            saveFs.WriteExtraData(in newExtraData).ThrowIfFailure();
            saveFs.Dispose();

            // Read extra data should match the first one
            CreateDirSaveFs(out saveFs, baseFs, true, true, true).ThrowIfFailure();
            Assert.Success(saveFs.ReadExtraData(out SaveDataExtraData extraData));

            Assert.Equal(originalExtraData, extraData);
        }

        [Fact]
        public void Initialize_InterruptedAfterCommitPart1_UsesWorkingExtraData()
        {
            var baseFs = new InMemoryFileSystem();

            CreateExtraDataForTest(baseFs, "/ExtraData_", 0x12345).ThrowIfFailure();
            CreateExtraDataForTest(baseFs, "/ExtraData1", 0x67890).ThrowIfFailure();

            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, true, true, true).ThrowIfFailure();

            saveFs.ReadExtraData(out SaveDataExtraData extraData).ThrowIfFailure();

            Assert.Equal(0x67890, extraData.DataSize);
        }

        [Fact]
        public void CommitSaveData_MultipleCommits_CommitIdIsUpdatedSkippingInvalidIds()
        {
            var random = new RandomGenerator();
            RandomDataGenerator randomGeneratorFunc = buffer => random.GenerateRandom(buffer);
            var timeStampGetter = new TimeStampGetter();

            var baseFs = new InMemoryFileSystem();
            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, timeStampGetter,
                 randomGeneratorFunc, true, true, true, null).ThrowIfFailure();

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out SaveDataExtraData extraData).ThrowIfFailure();
            Assert.Equal(2, extraData.CommitId);

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(3, extraData.CommitId);

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(6, extraData.CommitId);

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(2, extraData.CommitId);
        }

        [Fact]
        public void CommitSaveData_MultipleCommits_TimeStampUpdated()
        {
            var random = new RandomGenerator();
            RandomDataGenerator randomGeneratorFunc = buffer => random.GenerateRandom(buffer);
            var timeStampGetter = new TimeStampGetter();

            var baseFs = new InMemoryFileSystem();
            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, timeStampGetter,
                 randomGeneratorFunc, true, true, true, null).ThrowIfFailure();

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out SaveDataExtraData extraData).ThrowIfFailure();
            Assert.Equal(1u, extraData.TimeStamp);

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(2u, extraData.TimeStamp);

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(3u, extraData.TimeStamp);

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(4u, extraData.TimeStamp);
        }

        [Fact]
        public void CommitSaveData_UpdateTimeStampIsFalse_TimeStampAndCommitIdAreNotUpdated()
        {
            var random = new RandomGenerator();
            RandomDataGenerator randomGeneratorFunc = buffer => random.GenerateRandom(buffer);
            var timeStampGetter = new TimeStampGetter();

            var baseFs = new InMemoryFileSystem();
            CreateDirSaveFs(out DirectorySaveDataFileSystem saveFs, baseFs, timeStampGetter,
                 randomGeneratorFunc, true, true, true, null).ThrowIfFailure();

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out SaveDataExtraData extraData).ThrowIfFailure();
            Assert.Equal(1u, extraData.TimeStamp);
            Assert.Equal(2, extraData.CommitId);

            saveFs.CommitExtraData(false).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(1u, extraData.TimeStamp);
            Assert.Equal(2, extraData.CommitId);

            saveFs.CommitExtraData(true).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(2u, extraData.TimeStamp);
            Assert.Equal(3, extraData.CommitId);

            saveFs.CommitExtraData(false).ThrowIfFailure();
            saveFs.ReadExtraData(out extraData).ThrowIfFailure();
            Assert.Equal(2u, extraData.TimeStamp);
            Assert.Equal(3, extraData.CommitId);
        }

        private class TimeStampGetter : ISaveDataCommitTimeStampGetter
        {
            private long _currentTimeStamp = 1;

            public Result Get(out long timeStamp)
            {
                timeStamp = _currentTimeStamp++;
                return Result.Success;
            }
        }

        private class RandomGenerator
        {
            private static readonly int[] Values = { 2, 0, 3, 3, 6, 0 };

            private int _index;

            public Result GenerateRandom(Span<byte> output)
            {
                if (output.Length != 8)
                    throw new ArgumentException();

                Unsafe.As<byte, long>(ref MemoryMarshal.GetReference(output)) = Values[_index];

                _index = (_index + 1) % Values.Length;
                return Result.Success;
            }
        }

        private Result CreateExtraDataForTest(IFileSystem fileSystem, string path, int saveDataSize)
        {
            fileSystem.DeleteFile(path).IgnoreResult();

            Result rc = fileSystem.CreateFile(path, Unsafe.SizeOf<SaveDataExtraData>());
            if (rc.IsFailure()) return rc;

            var extraData = new SaveDataExtraData();
            extraData.DataSize = saveDataSize;

            using var file = new UniqueRef<IFile>();
            rc = fileSystem.OpenFile(ref file.Ref(), path, OpenMode.ReadWrite);
            if (rc.IsFailure()) return rc;

            using (file)
            {
                rc = file.Get.Write(0, SpanHelpers.AsByteSpan(ref extraData), WriteOption.Flush);
                if (rc.IsFailure()) return rc;
            }

            return Result.Success;
        }
    }
}