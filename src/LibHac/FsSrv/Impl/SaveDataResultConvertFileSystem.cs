using LibHac.Common;
using LibHac.Diag;
using LibHac.Fs;
using LibHac.Fs.Fsa;

namespace LibHac.FsSrv.Impl;

/// <summary>
/// Contains functions for converting internal save data <see cref="Result"/>s to external <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public static class SaveDataResultConvert
{
    private static Result ConvertCorruptedResult(Result result)
    {
        if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(result))
        {
            if (ResultFs.IncorrectIntegrityVerificationMagicCode.Includes(result))
                return ResultFs.IncorrectSaveDataIntegrityVerificationMagicCode.LogConverted(result);

            if (ResultFs.InvalidZeroHash.Includes(result))
                return ResultFs.InvalidSaveDataZeroHash.LogConverted(result);

            if (ResultFs.NonRealDataVerificationFailed.Includes(result))
                return ResultFs.SaveDataNonRealDataVerificationFailed.LogConverted(result);

            if (ResultFs.ClearedRealDataVerificationFailed.Includes(result))
                return ResultFs.ClearedSaveDataRealDataVerificationFailed.LogConverted(result);

            if (ResultFs.UnclearedRealDataVerificationFailed.Includes(result))
                return ResultFs.UnclearedSaveDataRealDataVerificationFailed.LogConverted(result);

            Assert.SdkAssert(false);
        }

        if (ResultFs.HostFileSystemCorrupted.Includes(result))
        {
            if (ResultFs.HostEntryCorrupted.Includes(result))
                return ResultFs.SaveDataHostEntryCorrupted.LogConverted(result);

            if (ResultFs.HostFileDataCorrupted.Includes(result))
                return ResultFs.SaveDataHostFileDataCorrupted.LogConverted(result);

            if (ResultFs.HostFileCorrupted.Includes(result))
                return ResultFs.SaveDataHostFileCorrupted.LogConverted(result);

            if (ResultFs.InvalidHostHandle.Includes(result))
                return ResultFs.InvalidSaveDataHostHandle.LogConverted(result);

            Assert.SdkAssert(false);
        }

        if (ResultFs.DatabaseCorrupted.Includes(result))
        {
            if (ResultFs.InvalidAllocationTableBlock.Includes(result))
                return ResultFs.InvalidSaveDataAllocationTableBlock.LogConverted(result);

            if (ResultFs.InvalidKeyValueListElementIndex.Includes(result))
                return ResultFs.InvalidSaveDataKeyValueListElementIndex.LogConverted(result);

            if (ResultFs.InvalidAllocationTableChainEntry.Includes(result))
                return ResultFs.InvalidSaveDataAllocationTableChainEntry.LogConverted(result);

            if (ResultFs.InvalidAllocationTableOffset.Includes(result))
                return ResultFs.InvalidSaveDataAllocationTableOffset.LogConverted(result);

            if (ResultFs.InvalidAllocationTableBlockCount.Includes(result))
                return ResultFs.InvalidSaveDataAllocationTableBlockCount.LogConverted(result);

            if (ResultFs.InvalidKeyValueListEntryIndex.Includes(result))
                return ResultFs.InvalidSaveDataKeyValueListEntryIndex.LogConverted(result);

            if (ResultFs.InvalidBitmapIndex.Includes(result))
                return ResultFs.InvalidSaveDataBitmapIndex.LogConverted(result);

            Assert.SdkAssert(false);
        }

        if (ResultFs.ZeroBitmapFileCorrupted.Includes(result))
        {
            if (ResultFs.IncompleteBlockInZeroBitmapHashStorageFile.Includes(result))
                return ResultFs.IncompleteBlockInZeroBitmapHashStorageFileSaveData.LogConverted(result);

            Assert.SdkAssert(false);
        }

        return result;
    }

    public static Result ConvertSaveFsDriverPrivateResult(Result result)
    {
        if (result.IsSuccess())
            return result;

        if (ResultFs.UnsupportedVersion.Includes(result))
            return ResultFs.UnsupportedSaveDataVersion.LogConverted(result);

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
            return ResultFs.PathNotFound.LogConverted(result);

        if (ResultFs.AllocationTableFull.Includes(result))
            return ResultFs.UsableSpaceNotEnough.LogConverted(result);

        if (ResultFs.AlreadyExists.Includes(result))
            return ResultFs.PathAlreadyExists.LogConverted(result);

        if (ResultFs.InvalidOffset.Includes(result))
            return ResultFs.OutOfRange.LogConverted(result);

        if (ResultFs.IncompatiblePath.Includes(result) ||
            ResultFs.FileNotFound.Includes(result))
        {
            return ResultFs.PathNotFound.LogConverted(result);
        }

        return result;
    }
}

/// <summary>
/// Wraps an <see cref="IFile"/>, converting its returned <see cref="Result"/>s
/// to save-data-specific <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class SaveDataResultConvertFile : IResultConvertFile
{
    public SaveDataResultConvertFile(ref UniqueRef<IFile> baseFile) : base(ref baseFile)
    {
    }

    protected override Result ConvertResult(Result result)
    {
        return SaveDataResultConvert.ConvertSaveFsDriverPrivateResult(result);
    }
}

/// <summary>
/// Wraps an <see cref="IDirectory"/>, converting its returned <see cref="Result"/>s
/// to save-data-specific <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class SaveDataResultConvertDirectory : IResultConvertDirectory
{
    public SaveDataResultConvertDirectory(ref UniqueRef<IDirectory> baseDirectory) : base(ref baseDirectory)
    {
    }

    protected override Result ConvertResult(Result result)
    {
        return SaveDataResultConvert.ConvertSaveFsDriverPrivateResult(result);
    }
}

/// <summary>
/// Wraps an <see cref="IFileSystem"/>, converting its returned <see cref="Result"/>s
/// to save-data-specific <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on FS 13.1.0 (nnSdk 13.4.0)</remarks>
public class SaveDataResultConvertFileSystem : IResultConvertFileSystem
{
    public SaveDataResultConvertFileSystem(ref SharedRef<IFileSystem> baseFileSystem)
        : base(ref baseFileSystem)
    {
    }

    protected override Result DoOpenFile(ref UniqueRef<IFile> outFile, in Path path, OpenMode mode)
    {
        using var file = new UniqueRef<IFile>();
        Result rc = ConvertResult(BaseFileSystem.Get.OpenFile(ref file.Ref(), path, mode));
        if (rc.IsFailure()) return rc;

        outFile.Reset(new SaveDataResultConvertFile(ref file.Ref()));
        return Result.Success;
    }

    protected override Result DoOpenDirectory(ref UniqueRef<IDirectory> outDirectory, in Path path,
        OpenDirectoryMode mode)
    {
        using var directory = new UniqueRef<IDirectory>();
        Result rc = ConvertResult(BaseFileSystem.Get.OpenDirectory(ref directory.Ref(), path, mode));
        if (rc.IsFailure()) return rc;

        outDirectory.Reset(new SaveDataResultConvertDirectory(ref directory.Ref()));
        return Result.Success;
    }

    protected override Result ConvertResult(Result result)
    {
        return SaveDataResultConvert.ConvertSaveFsDriverPrivateResult(result);
    }
}