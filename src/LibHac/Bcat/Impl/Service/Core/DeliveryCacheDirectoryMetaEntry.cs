using LibHac.Common.FixedArrays;

namespace LibHac.Bcat.Impl.Service.Core;

internal struct DeliveryCacheDirectoryMetaEntry
{
    public DirectoryName Name;
    public Digest Digest;
    public Array16<byte> Reserved;
}