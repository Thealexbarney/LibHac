using System;

namespace LibHac.Kvdb
{
    public interface IExportable
    {
        int ExportSize { get; }
        void ToBytes(Span<byte> output);
        void FromBytes(ReadOnlySpan<byte> input);
    }
}
