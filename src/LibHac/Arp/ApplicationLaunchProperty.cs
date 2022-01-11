namespace LibHac.Arp;

public struct ApplicationLaunchProperty
{
    public ApplicationId ApplicationId;
    public uint Version;
    public Ncm.StorageId StorageId;
    public Ncm.StorageId PatchStorageId;
    public ApplicationKind ApplicationKind;
}

public enum ApplicationKind : byte
{
    Application = 0,
    MicroApplication = 1
}