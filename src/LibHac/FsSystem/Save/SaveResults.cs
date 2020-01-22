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

            if (ResultFs.Result3002.Includes(result))
            {
                return ResultFs.Result4302.Value;
            }

            if (ResultFs.IvfcStorageCorrupted.Includes(result))
            {
                if (ResultFs.Result4602.Includes(result))
                {
                    return ResultFs.Result4362.Value;
                }

                if (ResultFs.Result4603.Includes(result))
                {
                    return ResultFs.Result4363.Value;
                }

                if (ResultFs.InvalidHashInIvfc.Includes(result))
                {
                    return ResultFs.InvalidHashInSaveIvfc.Value;
                }

                if (ResultFs.IvfcHashIsEmpty.Includes(result))
                {
                    return ResultFs.SaveIvfcHashIsEmpty.Value;
                }

                if (ResultFs.InvalidHashInIvfcTopLayer.Includes(result))
                {
                    return ResultFs.InvalidHashInSaveIvfcTopLayer.Value;
                }

                return result;
            }

            if (ResultFs.BuiltInStorageCorrupted.Includes(result))
            {
                if (ResultFs.Result4662.Includes(result))
                {
                    return ResultFs.Result4402.Value;
                }

                return result;
            }

            if (ResultFs.HostFsCorrupted.Includes(result))
            {
                if (description > 4701 && description < 4706)
                {
                    return new Result(ResultFs.ModuleFs, description - 260);
                }

                return result;
            }

            if (ResultFs.Range4811To4819.Includes(result))
            {
                if (ResultFs.Result4812.Includes(result))
                {
                    return ResultFs.Result4427.Value;
                }

                return result;
            }

            if (ResultFs.FileTableCorrupted.Includes(result))
            {
                if (description > 4721 && description < 4729)
                {
                    return new Result(ResultFs.ModuleFs, description - 260);
                }

                return result;
            }

            if (ResultFs.FatFsCorrupted.Includes(result))
            {
                return result;
            }

            if (ResultFs.EntryNotFound.Includes(result))
            {
                return ResultFs.PathNotFound.Value;
            }

            if (ResultFs.SaveDataPathAlreadyExists.Includes(result))
            {
                return ResultFs.PathAlreadyExists.Value;
            }

            if (ResultFs.PathNotFoundInSaveDataFileTable.Includes(result))
            {
                return ResultFs.PathNotFound.Value;
            }

            if (ResultFs.InvalidOffset.Includes(result))
            {
                return ResultFs.ValueOutOfRange.Value;
            }

            if (ResultFs.AllocationTableInsufficientFreeBlocks.Includes(result))
            {
                return ResultFs.InsufficientFreeSpace.Value;
            }

            return result;
        }
    }
}
