namespace LibHac.Bcat;

public struct DeliveryCacheDirectoryEntry
{
    public FileName Name;
    public long Size;
    public Digest Digest;

    public DeliveryCacheDirectoryEntry(ref readonly FileName name, long size, ref readonly Digest digest)
    {
        Name = name;
        Size = size;
        Digest = digest;
    }
}