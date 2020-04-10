using System;

namespace LibHac.Bcat.Detail.Ipc
{
    public interface IDeliveryCacheDirectoryService
    {
        Result Open(ref DirectoryName name);
        Result Read(out int entriesRead, Span<DeliveryCacheDirectoryEntry> entryBuffer);
        Result GetCount(out int count);
    }
}
