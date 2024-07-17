using System;
using LibHac.Common;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator;

public interface IMemoryStorageCreator
{
    public enum MemoryStorageId
    {
        UserPartitionFatFs,
        SignedSystemPartitionRaw,
        SystemPartitionFatFs,
        Id4,
        Count
    }

    Result Create(ref SharedRef<IStorage> outStorage, out Memory<byte> outBuffer, MemoryStorageId id);
    Result RegisterBuffer(MemoryStorageId id, Memory<byte> buffer);
}