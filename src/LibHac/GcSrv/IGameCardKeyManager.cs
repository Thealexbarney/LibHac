using System;

namespace LibHac.GcSrv;

public interface IGameCardKeyManager : IDisposable
{
    void PresetInternalKeys(ReadOnlySpan<byte> gameCardKey, ReadOnlySpan<byte> gameCardCertificate);
}