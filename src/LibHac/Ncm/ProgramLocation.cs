namespace LibHac.Ncm
{
    public readonly struct ProgramLocation
    {
        public readonly ProgramId ProgramId;
        public readonly StorageId StorageId;

        public ProgramLocation(ProgramId programId, StorageId storageId)
        {
            ProgramId = programId;
            StorageId = storageId;
        }
    }
}