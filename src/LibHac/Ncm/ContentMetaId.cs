namespace LibHac.Ncm
{
    public readonly struct ApplicationId
    {
        public readonly ulong Value;

        public ApplicationId(ulong value)
        {
            Value = value;
        }

        public static ApplicationId InvalidId => default;

        public static ApplicationId Start => new ApplicationId(0x0100000000010000);
        public static ApplicationId End => new ApplicationId(0x01FFFFFFFFFFFFFF);

        public static implicit operator ProgramId(ApplicationId id) => new ProgramId(id.Value);

        public static bool IsApplicationId(ProgramId programId)
        {
            return Start <= programId && programId <= End;
        }
    }

    public readonly struct PatchId
    {
        public readonly ulong Value;

        public PatchId(ulong value)
        {
            Value = value;
        }

        public static implicit operator ProgramId(PatchId id) => new ProgramId(id.Value);
    }

    public readonly struct DeltaId
    {
        public readonly ulong Value;

        public DeltaId(ulong value)
        {
            Value = value;
        }

        public static implicit operator ProgramId(DeltaId id) => new ProgramId(id.Value);
    }
}
