using System;
using LibHac.Fs;

namespace LibHac.FsSystem;

public interface IAesCtrDecryptor : IDisposable
{
    Result Decrypt(Span<byte> destination, ReadOnlySpan<byte> iv, ReadOnlySpan<byte> source);
    void PrioritizeSw();
    void SetExternalKeySource(in Spl.AccessKey keySource);
    AesCtrKeyTypeFlag GetKeyTypeFlag();
}