using System;

namespace LibHac.Bcat.Detail.Ipc
{
    public interface IDeliveryCacheDirectoryService
    {
        Result Open(ref DirectoryName name);
        Result Read(ref int entriesRead, Span<DeliveryCacheDirectoryEntry> entryBuffer);
        Result GetCount(ref int count);
    }
}
