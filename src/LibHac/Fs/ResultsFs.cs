namespace LibHac.Fs
{
    public class ResultsFs
    {
        public const int ModuleFs = 2;

        public static Result ResultFsPathNotFound => new Result(ModuleFs, 1);
        public static Result ResultFsMountNameAlreadyExists => new Result(ModuleFs, 60);
        public static Result ResultFsDifferentDestFileSystem => new Result(ModuleFs, 6034);
        public static Result ResultFsNullArgument => new Result(ModuleFs, 6063);
        public static Result ResultFsInvalidMountName => new Result(ModuleFs, 6065);
        public static Result ResultFsWriteStateUnflushed => new Result(ModuleFs, 6454);
        public static Result ResultFsWritableFileOpen => new Result(ModuleFs, 6457);
        public static Result ResultFsMountNameNotFound => new Result(ModuleFs, 6905);
    }
}
