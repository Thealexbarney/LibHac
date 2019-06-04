namespace LibHac.Kvdb
{
    public static class ResultsKvdb
    {
        public const int ModuleKvdb = 20;

        public static Result ResultKvdbTooLargeKey => new Result(ModuleKvdb, 1);
        public static Result ResultKvdbKeyNotFound => new Result(ModuleKvdb, 2);
        public static Result ResultKvdbAllocationFailed => new Result(ModuleKvdb, 4);
        public static Result ResultKvdbInvalidKeyValue => new Result(ModuleKvdb, 5);
        public static Result ResultKvdbBufferInsufficient => new Result(ModuleKvdb, 6);
    }
}
