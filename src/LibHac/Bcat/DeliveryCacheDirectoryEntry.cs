namespace LibHac.Bcat;

public struct DeliveryCacheDirectoryEntry
{
    public FileName Name;
    public long Size;
    public Digest Digest;

    public DeliveryCacheDirectoryEntry(in FileName name, long size, in Digest digest)
    {
        Name = name;
        Size = size;
        Digest = digest;
    }
}