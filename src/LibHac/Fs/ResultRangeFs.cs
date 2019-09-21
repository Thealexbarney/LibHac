namespace LibHac.Fs
{
    public static class ResultRangeFs
    {
        public static ResultRange DataCorrupted => new ResultRange(ResultFs.ModuleFs, 4000, 4999);
        public static ResultRange RomCorrupted => new ResultRange(ResultFs.ModuleFs, 4001, 4299);
        public static ResultRange SaveDataCorrupted => new ResultRange(ResultFs.ModuleFs, 4301, 4499);
        public static ResultRange NcaCorrupted => new ResultRange(ResultFs.ModuleFs, 4501, 4599);
        public static ResultRange IvfcStorageCorrupted => new ResultRange(ResultFs.ModuleFs, 4601, 4639);
        public static ResultRange PartitionFsCorrupted => new ResultRange(ResultFs.ModuleFs, 4641, 4659);
        public static ResultRange BuiltInStorageCorrupted => new ResultRange(ResultFs.ModuleFs, 4661, 4679);
        public static ResultRange FatFsCorrupted => new ResultRange(ResultFs.ModuleFs, 4681, 4699);
        public static ResultRange HostFsCorrupted => new ResultRange(ResultFs.ModuleFs, 4701, 4719);
        public static ResultRange FileTableCorrupted => new ResultRange(ResultFs.ModuleFs, 4721, 4739);
        public static ResultRange Range4811To4819 => new ResultRange(ResultFs.ModuleFs, 4811, 4819);

        public static ResultRange UnexpectedFailure => new ResultRange(ResultFs.ModuleFs, 5000, 5999);

        public static ResultRange EntryNotFound => new ResultRange(ResultFs.ModuleFs, 6600, 6699);
    }
}
