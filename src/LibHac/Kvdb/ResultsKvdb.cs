namespace LibHac.Kvdb
{
    public static class ResultsKvdb
    {
        public static readonly int ModuleKvdb = 20;

        public static readonly Result ResultKvdbTooLargeKey = new Result(ModuleKvdb, 1);
        public static readonly Result ResultKvdbKeyNotFound = new Result(ModuleKvdb, 2);
        public static readonly Result ResultKvdbAllocationFailed = new Result(ModuleKvdb, 4);
        public static readonly Result ResultKvdbInvalidKeyValue = new Result(ModuleKvdb, 5);
        public static readonly Result ResultKvdbBufferInsufficient = new Result(ModuleKvdb, 6);
    }
}
