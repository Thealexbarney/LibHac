﻿using System;
using LibHac.FsSystem;

namespace LibHac.FsService.Creators
{
    public interface IMemoryStorageCreator
    {
        Result Create(out IStorage storage, out Memory<byte> buffer, int storageId);
        Result RegisterBuffer(int storageId, Memory<byte> buffer);
    }
}
