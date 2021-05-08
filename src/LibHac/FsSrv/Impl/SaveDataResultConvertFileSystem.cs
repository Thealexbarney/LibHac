using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;
using LibHac.FsSystem;

namespace LibHac.FsSrv.Impl
{
    public static class SaveDataResultConvert
    {
        private static Result ConvertCorruptedResult(Result result)
        {
            if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(result))
            {
                if (ResultFs.IncorrectIntegrityVerificationMagic.Includes(result))
                    return ResultFs.IncorrectSaveDataIntegrityVerificationMagic.Value;

                if (ResultFs.InvalidZeroHash.Includes(result))
                    return ResultFs.InvalidSaveDataZeroHash.Value;

                if (ResultFs.NonRealDataVerificationFailed.Includes(result))
                    return ResultFs.SaveDataNonRealDataVerificationFailed.Value;

                if (ResultFs.ClearedRealDataVerificationFailed.Includes(result))
                    return ResultFs.ClearedSaveDataRealDataVerificationFailed.Value;

                if (ResultFs.UnclearedRealDataVerificationFailed.Includes(result))
                    return ResultFs.UnclearedSaveDataRealDataVerificationFailed.Value;

                Assert.SdkAssert(false);
            }

            if (ResultFs.HostFileSystemCorrupted.Includes(result))
            {
                if (ResultFs.HostEntryCorrupted.Includes(result))
                    return ResultFs.SaveDataHostEntryCorrupted.Value;

                if (ResultFs.HostFileDataCorrupted.Includes(result))
                    return ResultFs.SaveDataHostFileDataCorrupted.Value;

                if (ResultFs.HostFileCorrupted.Includes(result))
                    return ResultFs.SaveDataHostFileCorrupted.Value;

                if (ResultFs.InvalidHostHandle.Includes(result))
                    return ResultFs.InvalidSaveDataHostHandle.Value;

                Assert.SdkAssert(false);
            }

            if (ResultFs.DatabaseCorrupted.Includes(result))
            {
                if (ResultFs.InvalidAllocationTableBlock.Includes(result))
                    return ResultFs.InvalidSaveDataAllocationTableBlock.Value;

                if (ResultFs.InvalidKeyValueListElementIndex.Includes(result))
                    return ResultFs.InvalidSaveDataKeyValueListElementIndex.Value;

                if (ResultFs.AllocationTableIteratedRangeEntry.Includes(result))
                    return ResultFs.SaveDataAllocationTableIteratedRangeEntry.Value;

                if (ResultFs.InvalidAllocationTableOffset.Includes(result))
                    return ResultFs.InvalidSaveDataAllocationTableOffset.Value;

                if (ResultFs.InvalidAllocationTableBlockCount.Includes(result))
                    return ResultFs.InvalidSaveDataAllocationTableBlockCount.Value;

                if (ResultFs.InvalidKeyValueListEntryIndex.Includes(result))
                    return ResultFs.InvalidSaveDataKeyValueListEntryIndex.Value;

                if (ResultFs.InvalidBitmapIndex.Includes(result))
                    return ResultFs.InvalidSaveDataBitmapIndex.Value;

                Assert.SdkAssert(false);
            }

            if (ResultFs.ZeroBitmapFileCorrupted.Includes(result))
            {
                if (ResultFs.IncompleteBlockInZeroBitmapHashStorageFile.Includes(result))
                    return ResultFs.IncompleteBlockInZeroBitmapHashStorageFileSaveData.Value;

                Assert.SdkAssert(false);
            }

            return result;
        }

        public static Result ConvertSaveFsDriverPublicResult(Result result)
        {
            if (result.IsSuccess())
                return result;

            if (ResultFs.UnsupportedVersion.Includes(result))
                return ResultFs.UnsupportedSaveDataVersion.Value;

            if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(result) ||
                ResultFs.BuiltInStorageCorrupted.Includes(result) ||
                ResultFs.HostFileSystemCorrupted.Includes(result) ||
                ResultFs.DatabaseCorrupted.Includes(result) ||
                ResultFs.ZeroBitmapFileCorrupted.Includes(result))
            {
                return ConvertCorruptedResult(result);
            }

            if (ResultFs.FatFileSystemCorrupted.Includes(result))
                return result;

            if (ResultFs.NotFound.Includes(result))
                return ResultFs.PathNotFound.Value;

            if (ResultFs.AllocationTableFull.Includes(result))
                return ResultFs.UsableSpaceNotEnough.Value;

            if (ResultFs.AlreadyExists.Includes(result))
                return ResultFs.PathAlreadyExists.Value;

            if (ResultFs.InvalidOffset.Includes(result))
                return ResultFs.OutOfRange.Value;

            if (ResultFs.IncompatiblePath.Includes(result) ||
                ResultFs.FileNotFound.Includes(result))
            {
                return ResultFs.PathNotFound.Value;
            }

            return result;
        }
    }

    /// <summary>
    /// Wraps an <see cref="IFile"/>, converting its returned <see cref="Result"/>s
    /// to save-data-specific <see cref="Result"/>s.
    /// </summary>
    public class SaveDataResultConvertFile : IResultConvertFile
    {
        public SaveDataResultConvertFile(IFile baseFile) : base(baseFile)
        {
        }

        protected override Result ConvertResult(Result result)
        {
            return SaveDataResultConvert.ConvertSaveFsDriverPublicResult(result);
        }
    }

    /// <summary>
    /// Wraps an <see cref="IDirectory"/>, converting its returned <see cref="Result"/>s
    /// to save-data-specific <see cref="Result"/>s.
    /// </summary>
    public class SaveDataResultConvertDirectory : IResultConvertDirectory
    {
        public SaveDataResultConvertDirectory(IDirectory baseDirectory) : base(baseDirectory)
        {
        }

        protected override Result ConvertResult(Result result)
        {
            return SaveDataResultConvert.ConvertSaveFsDriverPublicResult(result);
        }
    }

    /// <summary>
    /// Wraps an <see cref="IFileSystem"/>, converting its returned <see cref="Result"/>s
    /// to save-data-specific <see cref="Result"/>s.
    /// </summary>
    public class SaveDataResultConvertFileSystem : IResultConvertFileSystem
    {
        public SaveDataResultConvertFileSystem(ref ReferenceCountedDisposable<IFileSystem> baseFileSystem)
            : base(ref baseFileSystem)
        {
        }

        public static ReferenceCountedDisposable<IFileSystem> CreateShared(
            ref ReferenceCountedDisposable<IFileSystem> baseFileSystem)
        {
            var resultConvertFileSystem = new SaveDataResultConvertFileSystem(ref baseFileSystem);
            return new ReferenceCountedDisposable<IFileSystem>(resultConvertFileSystem);
        }

        protected override Result DoOpenFile(out IFile file, U8Span path, OpenMode mode)
        {
            UnsafeHelpers.SkipParamInit(out file);

            Result rc = ConvertResult(BaseFileSystem.Target.OpenFile(out IFile tempFile, path, mode));
            if (rc.IsFailure()) return rc;

            file = new SaveDataResultConvertFile(tempFile);
            return Result.Success;
        }

        protected override Result DoOpenDirectory(out IDirectory directory, U8Span path, OpenDirectoryMode mode)
        {
            UnsafeHelpers.SkipParamInit(out directory);

            Result rc = ConvertResult(BaseFileSystem.Target.OpenDirectory(out IDirectory tempDirectory, path, mode));
            if (rc.IsFailure()) return rc;

            directory = new SaveDataResultConvertDirectory(tempDirectory);
            return Result.Success;
        }

        protected override Result ConvertResult(Result result)
        {
            return SaveDataResultConvert.ConvertSaveFsDriverPublicResult(result);
        }
    }

    /// <summary>
    /// Wraps an <see cref="ISaveDataExtraDataAccessor"/>, converting its returned <see cref="Result"/>s
    /// to save-data-specific <see cref="Result"/>s.
    /// </summary>
    public class SaveDataExtraDataResultConvertAccessor : ISaveDataExtraDataAccessor
    {
        private ReferenceCountedDisposable<ISaveDataExtraDataAccessor> _accessor;

        public SaveDataExtraDataResultConvertAccessor(
            ref ReferenceCountedDisposable<ISaveDataExtraDataAccessor> accessor)
        {
            _accessor = Shared.Move(ref accessor);
        }

        public static ReferenceCountedDisposable<ISaveDataExtraDataAccessor> CreateShared(
            ref ReferenceCountedDisposable<ISaveDataExtraDataAccessor> accessor)
        {
            var resultConvertAccessor = new SaveDataExtraDataResultConvertAccessor(ref accessor);
            return new ReferenceCountedDisposable<ISaveDataExtraDataAccessor>(resultConvertAccessor);
        }

        public void Dispose()
        {
            _accessor?.Dispose();
            _accessor = null;
        }

        public Result WriteExtraData(in SaveDataExtraData extraData)
        {
            Result rc = _accessor.Target.WriteExtraData(in extraData);
            return SaveDataResultConvert.ConvertSaveFsDriverPublicResult(rc);
        }

        public Result CommitExtraData(bool updateTimeStamp)
        {
            Result rc = _accessor.Target.CommitExtraData(updateTimeStamp);
            return SaveDataResultConvert.ConvertSaveFsDriverPublicResult(rc);
        }

        public Result ReadExtraData(out SaveDataExtraData extraData)
        {
            Result rc = _accessor.Target.ReadExtraData(out extraData);
            return SaveDataResultConvert.ConvertSaveFsDriverPublicResult(rc);
        }

        public void RegisterCacheObserver(ISaveDataExtraDataAccessorCacheObserver observer, SaveDataSpaceId spaceId,
            ulong saveDataId)
        {
            _accessor.Target.RegisterCacheObserver(observer, spaceId, saveDataId);
        }
    }
}