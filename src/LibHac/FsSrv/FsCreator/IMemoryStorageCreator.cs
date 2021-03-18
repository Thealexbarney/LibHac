using System;
using LibHac.Fs;

namespace LibHac.FsSrv.FsCreator
{
    public interface IMemoryStorageCreator
    {
        Result Create(out IStorage storage, out Memory<byte> buffer, int storageId);
        Result RegisterBuffer(int storageId, Memory<byte> buffer);
    }
}
