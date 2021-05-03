using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    internal static class SaveResults
    {
        public static Result ConvertToExternalResult(Result result)
        {
            int description = (int)result.Description;

            if (result == Result.Success)
            {
                return Result.Success;
            }

            if (result.Module != ResultFs.ModuleFs)
            {
                return result;
            }

            if (ResultFs.UnsupportedVersion.Includes(result))
            {
                return ResultFs.UnsupportedSaveDataVersion.Value;
            }

            if (ResultFs.IntegrityVerificationStorageCorrupted.Includes(result))
            {
                if (ResultFs.IncorrectIntegrityVerificationMagic.Includes(result))
                {
                    return ResultFs.IncorrectSaveDataIntegrityVerificationMagic.Value;
                }

                if (ResultFs.InvalidZeroHash.Includes(result))
                {
                    return ResultFs.InvalidSaveDataZeroHash.Value;
                }

                if (ResultFs.NonRealDataVerificationFailed.Includes(result))
                {
                    return ResultFs.SaveDataNonRealDataVerificationFailed.Value;
                }

                if (ResultFs.ClearedRealDataVerificationFailed.Includes(result))
                {
                    return ResultFs.ClearedSaveDataRealDataVerificationFailed.Value;
                }

                if (ResultFs.UnclearedRealDataVerificationFailed.Includes(result))
                {
                    return ResultFs.UnclearedSaveDataRealDataVerificationFailed.Value;
                }

                return result;
            }

            if (ResultFs.BuiltInStorageCorrupted.Includes(result))
            {
                if (ResultFs.InvalidGptPartitionSignature.Includes(result))
                {
                    return ResultFs.SaveDataGptHeaderSignatureVerificationFailed.Value;
                }

                return result;
            }

            if (ResultFs.HostFileSystemCorrupted.Includes(result))
            {
                if (description > 4701 && description < 4706)
                {
                    return new Result(ResultFs.ModuleFs, description - 260);
                }

                return result;
            }

            if (ResultFs.ZeroBitmapFileCorrupted.Includes(result))
            {
                if (ResultFs.IncompleteBlockInZeroBitmapHashStorageFile.Includes(result))
                {
                    return ResultFs.IncompleteBlockInZeroBitmapHashStorageFileSaveData.Value;
                }

                return result;
            }

            if (ResultFs.DatabaseCorrupted.Includes(result))
            {
                if (description > 4721 && description < 4729)
                {
                    return new Result(ResultFs.ModuleFs, description - 260);
                }

                return result;
            }

            if (ResultFs.FatFileSystemCorrupted.Includes(result))
            {
                return result;
            }

            if (ResultFs.NotFound.Includes(result))
            {
                return ResultFs.PathNotFound.Value;
            }

            if (ResultFs.AlreadyExists.Includes(result))
            {
                return ResultFs.PathAlreadyExists.Value;
            }

            if (ResultFs.IncompatiblePath.Includes(result))
            {
                return ResultFs.PathNotFound.Value;
            }

            if (ResultFs.InvalidOffset.Includes(result))
            {
                return ResultFs.OutOfRange.Value;
            }

            if (ResultFs.AllocationTableFull.Includes(result))
            {
                return ResultFs.UsableSpaceNotEnough.Value;
            }

            return result;
        }
    }
}
