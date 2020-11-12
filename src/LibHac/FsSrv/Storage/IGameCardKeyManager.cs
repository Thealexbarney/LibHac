using System;

namespace LibHac.FsSrv.Storage
{
    public interface IGameCardKeyManager
    {
        void PresetInternalKeys(ReadOnlySpan<byte> gameCardKey, ReadOnlySpan<byte> gameCardCertificate);
    }
}
