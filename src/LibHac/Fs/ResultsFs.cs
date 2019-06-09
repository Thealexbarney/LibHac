namespace LibHac.Fs
{
    public class ResultsFs
    {
        public const int ModuleFs = 2;

        public static Result ResultFsMountNameAlreadyExists => new Result(ModuleFs, 60);
        public static Result ResultFsWritableFileOpen => new Result(ModuleFs, 6457);
        public static Result ResultFsMountNameNotFound => new Result(ModuleFs, 6905);
    }
}
