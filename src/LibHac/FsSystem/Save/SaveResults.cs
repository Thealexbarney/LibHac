using LibHac.Fs;

namespace LibHac.FsSystem.Save
{
    internal static class SaveResults
    {
        public static Result ConvertToExternalResult(Result result)
        {
            int description = result.Description;

            if (result == Result.Success)
            {
                return Result.Success;
            }

            if (result.Module != ResultFs.ModuleFs)
            {
                return result;
            }

            if (result == ResultFs.Result3002)
            {
                return ResultFs.Result4302;
            }

            if (ResultRangeFs.IvfcStorageCorrupted.Contains(result))
            {
                if (result == ResultFs.Result4602)
                {
                    return ResultFs.Result4362;
                }

                if (result == ResultFs.Result4603)
                {
                    return ResultFs.Result4363;
                }

                if (result == ResultFs.InvalidHashInIvfc)
                {
                    return ResultFs.InvalidHashInSaveIvfc;
                }

                if (result == ResultFs.IvfcHashIsEmpty)
                {
                    return ResultFs.SaveIvfcHashIsEmpty;
                }

                if (result == ResultFs.InvalidHashInIvfcTopLayer)
                {
                    return ResultFs.InvalidHashInSaveIvfcTopLayer;
                }

                return result;
            }

            if (ResultRangeFs.BuiltInStorageCorrupted.Contains(result))
            {
                if (result == ResultFs.Result4662)
                {
                    return ResultFs.Result4402;
                }

                return result;
            }

            if (ResultRangeFs.HostFsCorrupted.Contains(result))
            {
                if (description > 4701 && description < 4706)
                {
                    return new Result(ResultFs.ModuleFs, description - 260);
                }

                return result;
            }

            if (ResultRangeFs.Range4811To4819.Contains(result))
            {
                if (result == ResultFs.Result4812)
                {
                    return ResultFs.Result4427;
                }

                return result;
            }

            if (ResultRangeFs.FileTableCorrupted.Contains(result))
            {
                if (description > 4721 && description < 4729)
                {
                    return new Result(ResultFs.ModuleFs, description - 260);
                }

                return result;
            }

            if (ResultRangeFs.FatFsCorrupted.Contains(result))
            {
                return result;
            }

            if (ResultRangeFs.EntryNotFound.Contains(result))
            {
                return ResultFs.PathNotFound;
            }

            if (result == ResultFs.SaveDataPathAlreadyExists)
            {
                return ResultFs.PathAlreadyExists;
            }

            if (result == ResultFs.PathNotFoundInSaveDataFileTable)
            {
                return ResultFs.PathNotFound;
            }

            if (result == ResultFs.InvalidOffset)
            {
                return ResultFs.ValueOutOfRange;
            }

            if (result == ResultFs.AllocationTableInsufficientFreeBlocks)
            {
                return ResultFs.InsufficientFreeSpace;
            }

            return result;
        }
    }
}
