using System;

namespace LibHac.GcSrv
{
    public interface IGameCardKeyManager
    {
        void PresetInternalKeys(ReadOnlySpan<byte> gameCardKey, ReadOnlySpan<byte> gameCardCertificate);
    }
}
