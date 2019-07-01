namespace LibHac.Kvdb
{
    public static class ResultKvdb
    {
        public const int ModuleKvdb = 20;

        public static Result TooLargeKey => new Result(ModuleKvdb, 1);
        public static Result KeyNotFound => new Result(ModuleKvdb, 2);
        public static Result AllocationFailed => new Result(ModuleKvdb, 4);
        public static Result InvalidKeyValue => new Result(ModuleKvdb, 5);
        public static Result BufferInsufficient => new Result(ModuleKvdb, 6);
    }
}
