using LibHac.Diag;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator;

/// <summary>
/// Contains functions for converting internal save data <see cref="Result"/>s to external <see cref="Result"/>s.
/// </summary>
/// <remarks>Based on FS 14.1.0 (nnSdk 14.3.0)</remarks>
public static class SaveDataResultConverter
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
        else if (ResultFs.HostFileSystemCorrupted.Includes(result))
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
        else if (ResultFs.DatabaseCorrupted.Includes(result))
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
        else if (ResultFs.ZeroBitmapFileCorrupted.Includes(result))
        {
            if (ResultFs.IncompleteBlockInZeroBitmapHashStorageFile.Includes(result))
                return ResultFs.IncompleteBlockInZeroBitmapHashStorageFileSaveData.LogConverted(result);

            Assert.SdkAssert(false);
        }

        return result;
    }

    private static Result ConvertResult(Result result)
    {
        if (ResultFs.UnsupportedVersion.Includes(result))
            return ResultFs.UnsupportedSaveDataVersion.LogConverted(result);

        if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(result) ||
            ResultFs.BuiltInStorageCorrupted.Includes(result) ||
            ResultFs.HostFileSystemCorrupted.Includes(result) ||
            ResultFs.DatabaseCorrupted.Includes(result) ||
            ResultFs.ZeroBitmapFileCorrupted.Includes(result))
        {
            return ConvertCorruptedResult(result).Miss();
        }

        if (ResultFs.FatFileSystemCorrupted.Includes(result))
            return result;

        if (ResultFs.NotFound.Includes(result))
            return ResultFs.PathNotFound.LogConverted(result);

        if (ResultFs.AllocationTableFull.Includes(result))
            return ResultFs.UsableSpaceNotEnough.LogConverted(result);

        if (ResultFs.AlreadyExists.Includes(result))
            return ResultFs.PathAlreadyExists.LogConverted(result);

        if (ResultFs.IncompatiblePath.Includes(result) ||
            ResultFs.FileNotFound.Includes(result))
        {
            return ResultFs.PathNotFound.LogConverted(result);
        }

        return result;
    }

    public static Result ConvertSaveDataFsResult(Result result, bool isReconstructible)
    {
        if (result.IsSuccess())
            return Result.Success;

        Result convertedResult = ConvertResult(result);

        if (isReconstructible && ResultFs.SaveDataCorrupted.Includes(convertedResult))
        {
            return ResultFs.ReconstructibleSaveDataCorrupted.LogConverted(convertedResult);
        }

        return convertedResult;
    }
}