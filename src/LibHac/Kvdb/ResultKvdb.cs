namespace LibHac.Kvdb
{
    public static class ResultKvdb
    {
        public const int ModuleKvdb = 20;

        public static Result.Base TooLargeKeyOrDbFull => new Result.Base(ModuleKvdb, 1);
        public static Result.Base KeyNotFound => new Result.Base(ModuleKvdb, 2);
        public static Result.Base AllocationFailed => new Result.Base(ModuleKvdb, 4);
        public static Result.Base InvalidKeyValue => new Result.Base(ModuleKvdb, 5);
        public static Result.Base BufferInsufficient => new Result.Base(ModuleKvdb, 6);
    }
}
