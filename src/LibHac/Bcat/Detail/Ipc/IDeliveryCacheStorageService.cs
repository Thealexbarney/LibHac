using System;

namespace LibHac.Bcat.Detail.Ipc
{
    public interface IDeliveryCacheStorageService
    {
        Result CreateFileService(out IDeliveryCacheFileService fileService);
        Result CreateDirectoryService(out IDeliveryCacheDirectoryService directoryService);
        Result EnumerateDeliveryCacheDirectory(out int namesRead, Span<DirectoryName> nameBuffer);
    }
}
