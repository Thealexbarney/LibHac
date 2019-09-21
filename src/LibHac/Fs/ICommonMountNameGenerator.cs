using System;

namespace LibHac.Fs
{
    public interface ICommonMountNameGenerator
    {
        Result Generate(Span<byte> nameBuffer);
    }
}