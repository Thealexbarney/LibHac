using System;

namespace LibHac.FsSystem;

public interface IMacGenerator
{
    Result Generate(Span<byte> macDestBuffer, ReadOnlySpan<byte> data);
}