using LibHac.Common.FixedArrays;

namespace LibHac.Bcat.Impl.Service.Core;

internal struct DeliveryCacheFileMetaEntry
{
    public FileName Name;
    public long Id;
    public long Size;
    public Digest Digest;
    public Array64<byte> Reserved;
}